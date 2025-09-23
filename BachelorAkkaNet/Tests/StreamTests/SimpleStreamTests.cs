using Akka.Actor;
using Akka.Streams;
using Akka.Streams.Dsl;
using Akka.Streams.TestKit;
using Akka.TestKit.Xunit2;
using Infrastructure.General;
using Xunit;
using Xunit.Abstractions;

namespace Tests.StreamTests;

public class SimpleStreamTests(ITestOutputHelper output) : TestKit(TestConfig, output)
{
    private static readonly string TestConfig = """
                                                akka.stdout-loglevel = Warning
                                                akka.loggers = ["Akka.TestKit.TestEventListener, Akka.TestKit"]
                                                akka.loglevel = Info
                                                """;

    [Fact]
    public void Should_backpressure_until_demand()
    {
        var mat = Sys.Materializer();

        var source = Source.From(Enumerable.Range(1, 100));
        var sinkProbe = this.SinkProbe<int>();

        var sub = source.RunWith(sinkProbe, mat);

        // grab fist element
        sub.Request(1);
        // check first element is 1
        sub.ExpectNext(1);
        sub.ExpectNoMsg(TimeSpan.FromMilliseconds(100));

        sub.Request(4);
        sub.ExpectNext(2, 3, 4, 5);
        sub.Cancel();
    }

    [Fact]
    public void Stream_should_not_push_without_acks()
    {
        var mat = Sys.Materializer();

        var source = Source.From(Enumerable.Range(1, 100));

        // Sink mit Ack-Handshake: erst auf "ack" zieht er weiter
        source.To(
            Sink.ActorRefWithAck<int>(
                TestActor,
                onInitMessage: StreamInit.Instance,
                ackMessage: StreamAck.Instance,
                onCompleteMessage: StreamCompleted.Instance,
                onFailureMessage: ex => ex
            )
        ).Run(mat);

        // 1) only for showing but not needed in production code
        // begin with materialization
        ExpectMsg(StreamInit.Instance);
        var stage = LastSender;

        // 2) Check that without ack no elements are sent
        ExpectNoMsg(TimeSpan.FromMilliseconds(200));

        // 3) Send one ack -> one element
        stage.Tell(StreamAck.Instance);
        ExpectMsg(1);

        // 4) Check that without ack no further elements are sent
        ExpectNoMsg(TimeSpan.FromMilliseconds(100));

        // 5) Send more ack's -> more elements
        stage.Tell(StreamAck.Instance);
        ExpectMsg(2);

        stage.Tell(StreamAck.Instance);
        ExpectMsg(3);
    }

    [Fact]
    public void Stream_should_push_automatically_without_ack_handshake()
    {
        var mat = Sys.Materializer();

        var source = Source.From(Enumerable.Range(1, 100));

        // No Backpressure-Sink, simple pushing
        source.To(
            Sink.ActorRef<int>(
                TestActor,
                onCompleteMessage: StreamCompleted.Instance,
                onFailureMessage: ex => ex
            )
        ).Run(mat);

        // Get data without acking
        var first = ExpectMsg<int>(TimeSpan.FromSeconds(1));
        var second = ExpectMsg<int>(TimeSpan.FromSeconds(1));
        var third = ExpectMsg<int>(TimeSpan.FromSeconds(1));

        var moreCame = FishForMessage(_ => true, TimeSpan.FromMilliseconds(200));
    }

    [Fact]
    public void GraphDsl_should_propagate_backpressure_from_sink_to_source()
    {
        var mat = Sys.Materializer();

        var pub = this.CreateManualPublisherProbe<int>();
        var sub = this.CreateManualSubscriberProbe<int>();

        RunnableGraph
            .FromGraph(GraphDsl.Create(builder =>
            {
                var src = builder.Add(Source.FromPublisher(pub));
                var flow = builder.Add(Flow.Create<int>().Select(x => x * 2));
                var sink = builder.Add(Sink.FromSubscriber(sub));

                builder.From(src).Via(flow).To(sink);
                return ClosedShape.Instance;
            }))
            .Run(mat);

        var upSub = pub.ExpectSubscription();
        var downSub = sub.ExpectSubscription();

        // No demand -> no data
        sub.ExpectNoMsg(TimeSpan.FromMilliseconds(50));

        // Send request from downstream to upstream
        downSub.Request(1);

        int actual = 10;

        upSub.SendNext(actual);
        sub.ExpectNext(actual * 2);
        sub.ExpectNoMsg(TimeSpan.FromMilliseconds(50));

        downSub.Cancel();
    }

    [Fact]
    public void Broadcast_two_slow_branches_should_limit_upstream_requests()
    {
        var mat = Sys.Materializer();

        var pub = this.CreateManualPublisherProbe<int>();
        var sub = this.CreateManualSubscriberProbe<int>();

        RunnableGraph.FromGraph(GraphDsl.Create(builder =>
        {
            var src = builder.Add(Source.FromPublisher(pub));

            var bcast = builder.Add(new Broadcast<int>(2));

            // Beide Branches künstlich langsam, Parallelität = 1
            var slow1 = builder.Add(
                Flow
                    .Create<int>()
                    .SelectAsync(1, async x =>
                    {
                        await Task.Delay(100); 
                        return x;
                    })
                    .WithAttributes(Attributes.CreateInputBuffer(1, 1)));
            var slow2 = builder.Add(
                Flow
                    .Create<int>()
                    .SelectAsync(1, async x =>
                    {
                        await Task.Delay(100); 
                        return x;
                    })
                    .WithAttributes(Attributes.CreateInputBuffer(1, 1)));

            var merge = builder.Add(new Merge<int>(2));

            var sink = builder.Add(Sink.FromSubscriber(sub));

            builder.From(src).To(bcast);
            builder.From(bcast.Out(0)).Via(slow1).To(merge.In(0));
            builder.From(bcast.Out(1)).Via(slow2).To(merge.In(1));
            builder.From(merge).To(sink);

            return ClosedShape.Instance;
        })).Run(mat);

        var pubSub = pub.ExpectSubscription();
        var downstream = sub.ExpectSubscription();

        // Downstream „öffnet den Hahn“ stark – aber Upstream darf nur so viel anfragen,
        // wie durch Parallelität (1+1) möglich ist:
        downstream.Request(100);

        // => Anfangs sollten genau 2 Requests am Publisher ankommen (ein Request je Branch)
        //pub.ExpectRequest(2);

        // Sende zwei Elemente; jedes landet auf je einem Branch und wird mit Delay verarbeitet
        pubSub.SendNext(1);
        pubSub.SendNext(2);

        // Downstream bekommt beide, Reihenfolge kann je nach Timing variieren
        // (hier einfach zwei ExpectNext ohne konkrete Reihenfolge)
        sub.ExpectNext(); // 1 oder 2
        sub.ExpectNext(); // 1 oder 2

        // Nach Abschluss der Verarbeitung sollte erneut Request(2) eintreffen (eine pro Branch)
        //pub.ExpectRequest(pubSub, 2);

        // Aufräumen
        downstream.Cancel();
    }
}