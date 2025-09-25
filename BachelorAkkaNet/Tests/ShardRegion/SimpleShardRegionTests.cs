using Akka.Event;
using Akka.TestKit.Xunit2;
using Xunit.Abstractions;

namespace Tests.ShardRegion;

public class SimpleShardRegionTests(ITestOutputHelper helper) : TestKit(TestConfig,helper)
{
    private static readonly string TestConfig = """
                                                akka.stdout-loglevel = Warning
                                                akka.loggers = ["Akka.TestKit.TestEventListener, Akka.TestKit"]
                                                akka.loglevel = Info
                                                """;

    private ILoggingAdapter Logger => Sys.Log;
}