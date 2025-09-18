using Akka.Streams;
using Akka.Streams.Dsl;
using Akka.TestKit.Xunit2;
using Akka.Util;
using Xunit;
using Xunit.Abstractions;

namespace Tests.Streams;

public class SimpleAkkaStreamTests(ITestOutputHelper output) : TestKit(TestConfig, output)
{
    private static readonly string TestConfig = """
                                                akka.stdout-loglevel = Warning
                                                akka.loggers = ["Akka.TestKit.TestEventListener, Akka.TestKit"]
                                                akka.loglevel = Info
                                                """;

    [Fact]
    public async Task TestAkkaStreamsSimpleStream()
    {
        int startTs = 0;
        var source = Source
            .Tick(TimeSpan.Zero, TimeSpan.FromMilliseconds(100), 1)
            .Scan(startTs, (l, _) => l + 1)
            
            .Select(l => l)
            .Buffer(2, OverflowStrategy.DropHead)
            .To(Sink.ForEach<int>(l => Output.WriteLine("Message {0}", l)));

        source.Run(Sys.Materializer());

        await Task.Delay(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public Task Test_Akka_Stream_Check_Source_Options()
    {
        
    }
}