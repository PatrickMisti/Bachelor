using Akka.Event;
using Akka.TestKit.Xunit2;
using Xunit.Abstractions;

namespace Tests.ShardRegion;

public class SimpleShardRegionTests(ITestOutputHelper helper) : TestKit(TestConfig,helper)
{
    private static readonly string TestConfig = """
                                                akka {
                                                  loglevel = "INFO"                         # nur INFO+ überhaupt posten
                                                  stdout-loglevel = "INFO"
                                                  loggers = [
                                                    "Akka.TestKit.TestEventListener, Akka.TestKit",   # -> xUnit-Output via helper
                                                    "Akka.Event.StandardOutLogger, Akka"              # (optional) zusätzlich Konsole
                                                  ]
                                                }
                                                """;

    private ILoggingAdapter Logger => Sys.Log;

}