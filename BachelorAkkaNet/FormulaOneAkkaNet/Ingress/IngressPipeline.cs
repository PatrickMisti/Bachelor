using Akka;
using Akka.Actor;
using Akka.Event;
using Akka.Streams;
using Akka.Streams.Dsl;
using FormulaOneAkkaNet.Ingress.Messages;
using Infrastructure.Http;

namespace FormulaOneAkkaNet.Ingress;

public class IngressPipeline
{
    private readonly ActorSystem _system;
    private readonly IMaterializer _mat;
    private readonly IActorRef _driverProxy;
    private readonly ILoggingAdapter _log;

    private ISourceQueueWithComplete<IOpenF1Dto>? _queue;
    private IKillSwitch? _kill;
    private ICancelable? _pollMat;

    private bool _running;
    private Mode _mode = Mode.None;


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

    /// <summary>Start Queue-based push mode.</summary>
    public void StartPush(int workerCount = 4)
    {
        StopInternal();

        var workers = SpawnWorkers(workerCount);

        // Import BOTH: Source.Queue (mat: ISourceQueueWithComplete) and KillSwitches.Single (mat: IKillSwitch)
        var source = Source.Queue<IOpenF1Dto>(bufferSize: 8192, overflowStrategy: OverflowStrategy.Backpressure);
        var kill = KillSwitches.Single<IOpenF1Dto>(); // IGraph<FlowShape<T,T>, IKillSwitch>

        // Combine both mats into a tuple (queue, ks) and wire: src -> ks -> balance -> sinks
        var graph = GraphDsl.Create<
            ClosedShape,
            (ISourceQueueWithComplete<IOpenF1Dto> Queue, IKillSwitch Ks),
            ISourceQueueWithComplete<IOpenF1Dto>,
            IKillSwitch,
            SourceShape<IOpenF1Dto>,
            FlowShape<IOpenF1Dto, IOpenF1Dto>
        >(
            source,
            kill,
            combineMaterializers: (q, killer) => (q, killer),
            buildBlock: (builder, src, killer) =>
            {
                var balance = builder.Add(new Balance<IOpenF1Dto>(workers.Count, waitForAllDownstreams: true));

                builder.From(src).Via(killer).To(balance.In);

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
        _log.Info("IngressPipeline started in PUSH mode with {0} workers.", workerCount);
    }

    /// <summary>Start polling mode (Tick + FetchNextBatch).</summary>
    public void StartPolling(IHttpWrapperClient pollClient, TimeSpan interval, int sessionKey, int workerCount = 4)
    {
        StopInternal();

        var workers = SpawnWorkers(workerCount);

        // Polling source materializes ICancelable (the Tick), we don't really need to keep it
        var pollingSource =
            Source.Tick(TimeSpan.Zero, interval, NotUsed.Instance)
                  .SelectAsync(1, async _ =>
                  {
                      var batch = await pollClient.FetchNextBatch(sessionKey, CancellationToken.None).ConfigureAwait(false);
                      return batch ?? Array.Empty<IOpenF1Dto>();
                  })
                  .SelectMany(batch => batch);

        var kill = KillSwitches.Single<IOpenF1Dto>();

        // Combine mats, but only keep the IKillSwitch as our materialized value (ignore ICancelable)
        var graph = GraphDsl.Create<
            ClosedShape,
            IKillSwitch,
            ICancelable,
            IKillSwitch,
            SourceShape<IOpenF1Dto>,
            FlowShape<IOpenF1Dto, IOpenF1Dto>
        >(
            pollingSource,
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
        _log.Info("IngressPipeline started in POLLING mode every {0} with {1} workers.", interval, workerCount);
    }

    public void Stop()
    {
        StopInternal();
        _log.Info("IngressPipeline: stopped.");
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
            await _queue.OfferAsync(dto).ConfigureAwait(false);
    }

    // ---------- intern ----------

    private void StopInternal()
    {
        _queue?.Complete();
        _queue = null;

        _kill?.Shutdown();
        _kill = null;

        _pollMat?.Cancel();
        _pollMat = null;

        _running = false;
        _mode = Mode.None;
    }

    private List<IActorRef> SpawnWorkers(int workerCount)
    {
        var list = new List<IActorRef>(workerCount);
        for (var i = 0; i < workerCount; i++)
        {
            var w = _system.ActorOf(Props.Create(() => new IngressWorkerActor(_driverProxy)), $"ingress-worker-{i + 1}");
            list.Add(w);
        }
        return list;
    }
}
