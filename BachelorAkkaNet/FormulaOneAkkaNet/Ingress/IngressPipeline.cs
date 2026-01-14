using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Routing;
using Akka.Streams;
using Akka.Streams.Dsl;
using Infrastructure.General;
using Infrastructure.Http;
using Infrastructure.PubSub;
using Infrastructure.ShardRegion.Messages;

namespace FormulaOneAkkaNet.Ingress;

public class IngressPipeline
{
    private readonly ActorSystem _system;
    private readonly IMaterializer _mat;
    private readonly IActorRef _driverProxy;
    private readonly ILoggingAdapter _log;

    private ISourceQueueWithComplete<IOpenF1Dto>? _queue;
    private IKillSwitch? _kill;

    private List<IActorRef> _workers = new();

    private bool _running;
    private Mode _mode = Mode.Push;


    public IngressPipeline(ActorSystem system, IMaterializer mat, IActorRef driverProxy)
    {
        _system = system;
        _mat = mat;
        _driverProxy = driverProxy;
        _log = Logging.GetLogger(_system, nameof(IngressPipeline));
    }

    public bool IsRunning => _running;
    public bool IsPushMode => _mode == Mode.Push;
    public bool IsPollingMode => _mode == Mode.Polling;
    public Mode GetMode => _mode;
    public void ChangeMode(Mode mode) => _mode = mode;

    /// <summary>Start Queue-based push mode.</summary>
    public void StartPush(int workerCount = 4)
    {
        StopInternal();

        var workers = SpawnRouter(workerCount);

        var (queue, source) = Source
            .Queue<IOpenF1Dto>(bufferSize: 8192, overflowStrategy: OverflowStrategy.Backpressure)
                .PreMaterialize(_mat);

        var (ks, done) = source
            .ViaMaterialized(KillSwitches.Single<IOpenF1Dto>(), Keep.Right)
            .SelectAsyncUnordered(workerCount, async dto => await workers.Ask<StreamAck>(dto))
            .Recover(ex =>
            {
                _log.Error(ex, "Stream failed, recovering and completing.");
                //_system.PubSub().Api.Publish(new NotifyStatusFailureMessage(ex.Message));
                return StreamAck.Instance;
            })
            .ToMaterialized(Sink.Ignore<StreamAck>(), Keep.Both)
            .Run(_mat);
        
        _queue = queue;
        _kill = ks;
        _mode = Mode.Push;
        _running = true;
        _log.Debug("IngressPipeline started in PUSH mode with {0} workers.", workerCount);
    }

    public void StartPushDsl(int workerCount = 4)
    {
        StopInternal();

        var workers = SpawnWorkers(workerCount);

        var source = Source
            .Queue<IOpenF1Dto>(bufferSize: 8192, overflowStrategy: OverflowStrategy.Backpressure);

        var restartable =
            RestartFlow.WithBackoff<IOpenF1Dto, IOpenF1Dto, NotUsed>(
                Flow.Create<IOpenF1Dto>,
                RestartSettings.Create(
                    minBackoff: TimeSpan.FromSeconds(1),
                    maxBackoff: TimeSpan.FromSeconds(30),
                    randomFactor: 0.2));

        var graph = GraphDsl.Create(
            source,
            KillSwitches.Single<IOpenF1Dto>(),
            (q, kill) => (q, kill),
            (builder, src, kill) =>
            {
                var balance = builder.Add(new Balance<IOpenF1Dto>(workers.Count, waitForAllDownstreams: false));

                builder.From(src).Via(kill).Via(restartable).To(balance.In);

                for (int i = 0; i < workers.Count; i++)
                {
                    var sink = Sink.ActorRefWithAck<IOpenF1Dto>(
                        workers[i],
                        onInitMessage: StreamInit.Instance,
                        ackMessage: StreamAck.Instance,
                        onCompleteMessage: StreamCompleted.Instance);

                    builder.From(balance.Out(i)).To(builder.Add(sink));
                }

                return ClosedShape.Instance;
            });

        var (queue, ks) = RunnableGraph.FromGraph(graph).Run(_mat);

        _queue = queue;
        _kill = ks;
        _mode = Mode.Push;
        _running = true;
        _log.Debug("IngressPipeline started in PUSH mode with {0} workers.", workerCount);
    }


/*
 *<
       ClosedShape,
       (ISourceQueueWithComplete<IOpenF1Dto> Queue, IKillSwitch Ks),
       ISourceQueueWithComplete<IOpenF1Dto>,
       IKillSwitch,
       SourceShape<IOpenF1Dto>,
       FlowShape<IOpenF1Dto, IOpenF1Dto>
   >
 */

    /// <summary>Start polling mode (Tick + FetchNextBatch).</summary>
    public void StartPolling(IHttpWrapperClient pollClient, int sessionKey, int workerCount = 4)
    {
        StopInternal();
        var workers = SpawnWorkers(workerCount);

        // Polling Simulation - preload and then replay at a fixed cadence
        var preloadTask = Task.Run(async () =>
        {
            try
            {
                var positionsTask = pollClient.GetPositionsOnTrackAsync(sessionKey);
                var intervalsTask = pollClient.GetIntervalDriversAsync(sessionKey);

                await Task.WhenAll(positionsTask, intervalsTask).ConfigureAwait(false);

                var positions = positionsTask.Result ?? Array.Empty<PositionOnTrackDto>();
                var intervals = intervalsTask.Result ?? Array.Empty<IntervalDriverDto>();

                var combined = new List<IOpenF1DtoWithDate>(positions.Count + intervals.Count);
                combined.AddRange(positions);
                combined.AddRange(intervals);

                combined.Sort(static (a, b) => a.CurrentDateTime.CompareTo(b.CurrentDateTime));
                return combined;
            }
            catch (Exception ex)
            {
                _log.Error(ex, "Preload failed for session {0}", sessionKey);
                return new List<IOpenF1DtoWithDate>(0);
            }
        });

        var preload = Source.FromTask(preloadTask);

        // Socket simulation
        var tick = TimeSpan.FromMilliseconds(50);
        var batchSize = 20;

        bool withBatching = false;

        // Emit in batches on a fixed cadence and complete after the list is exhausted
        var preloadedTickSource = preload.ConcatMany(list =>
            withBatching
                ? Source.From(list)
                    .Grouped(batchSize)
                    .Throttle(1, tick, 1, ThrottleMode.Shaping)
                    .SelectMany(batch => batch.Select(x => (IOpenF1Dto)x))
                    .MapMaterializedValue(_ => NotUsed.Instance)
                : Source.From(list)
                    .Select(x => (IOpenF1Dto)x)
                    .MapMaterializedValue(_ => NotUsed.Instance)
        );

        // Polling source materializes NotUsed; we only keep the kill switch
        var kill = KillSwitches.Single<IOpenF1Dto>();

        // Combine mats, but only keep the IKillSwitch as our materialized value
        var graph = GraphDsl.Create(
            preloadedTickSource,
            kill,
            combineMaterializers: (_, ks) => ks,
            buildBlock: (builder, src, ks) =>
            {
                var balance = builder.Add(new Balance<IOpenF1Dto>(workers.Count, waitForAllDownstreams: true));

                builder.From(src).Via(ks).To(balance.In);

                for (int i = 0; i < workers.Count; i++)
                {
                    var sink = Sink.ActorRefWithAck<IOpenF1Dto>(
                        workers[i],
                        onInitMessage: StreamInit.Instance,
                        ackMessage: StreamAck.Instance,
                        onCompleteMessage: StreamCompleted.Instance);

                    builder.From(balance.Out(i)).To(builder.Add(sink));
                }

                return ClosedShape.Instance;
            });

        _kill = RunnableGraph.FromGraph(graph).Run(_mat);
        _mode = Mode.Polling;
        _running = true;
        _log.Info("IngressPipeline started in POLLING mode every {0} with {1} workers.", tick, workerCount);
    }

    public async Task<bool> OfferAsync(IOpenF1Dto dto)
    {
        if (_mode != Mode.Push || _queue is null) return false;

        var result = await _queue.OfferAsync(dto).ConfigureAwait(false);
        return result switch
        {
            QueueOfferResult.Enqueued => true,
            QueueOfferResult.Dropped => false,
            QueueOfferResult.Failure => false,
            QueueOfferResult.QueueClosed => false,
            _ => false
        };
    }

    public async Task OfferAsync(IList<IOpenF1Dto> list)
    {
        if (_mode != Mode.Push || _queue is null) return;

        foreach (var dto in list)
        {
            var result = await _queue.OfferAsync(dto).ConfigureAwait(false);
            if (result is not QueueOfferResult.Enqueued)
            {
                _log.Warning("Offer result was {0}. Stopping batch enqueue.", result);
                if (result is QueueOfferResult.QueueClosed or QueueOfferResult.Failure) break;
            }
        }
    }
    public void Stop()
    {
        StopInternal();
        _log.Info("IngressPipeline: stopped.");
    }

    // ---------- intern ----------

    private void StopInternal()
    {
        _queue?.Complete();
        _queue = null;

        _kill?.Shutdown();
        _kill = null;

        foreach (var w in _workers)
            _system.Stop(w);
        _workers.Clear();

        _running = false;
        //_mode = Mode.None;
    }

    private List<IActorRef> SpawnWorkers(int workerCount)
    {
        var list = new List<IActorRef>(workerCount);
        for (var i = 0; i < workerCount; i++)
            list.Add(_system.ActorOf(
                IngressWorkerActor.Prop(_driverProxy),
                $"ingress-worker-{i + 1}-{Guid.NewGuid()}"
            ));

        _workers = list;

        return list;
    }

    private IActorRef SpawnRouter(int workerCount)
    {
        var router = _system.ActorOf(
            IngressWorkerActor.Prop(_driverProxy)
                .WithRouter(new RoundRobinPool(workerCount)),
            $"ingress-worker-router-{Guid.NewGuid()}"
        );

        return router;
    }
}
