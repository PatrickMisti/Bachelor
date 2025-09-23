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
}