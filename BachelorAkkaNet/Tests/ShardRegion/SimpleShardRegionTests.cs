using Akka.Actor;
using Akka.Event;
using Akka.TestKit.Xunit2;
using Infrastructure.ShardRegion;
using Tests.ShardRegion.Assets;
using Tests.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Tests.ShardRegion;

public class SimpleShardRegionTests(ITestOutputHelper helper) : TestKit(TestConfig, helper)
{
    private static readonly string TestConfig = """
                                                akka {
                                                  loglevel = "INFO"
                                                  stdout-loglevel = "INFO"
                                                  actor.provider = "cluster"
                                                  remote.dot-netty.tcp {
                                                    hostname = "127.0.0.1"
                                                    port = 0
                                                  }
                                                  cluster.min-nr-of-members = 1
                                                  cluster.roles = ["test"]
                                                  loggers = [
                                                    "Akka.TestKit.TestEventListener, Akka.TestKit",
                                                    "Akka.Event.StandardOutLogger, Akka"
                                                  ]
                                                }
                                                """;

    private ILoggingAdapter Logger => Sys.Log;

    [Fact]
    public async Task ShardRegion_should_initialize_entity_and_echo_responses()
    {
        var region = await ShardRegionTools.EnsureRegionStarted(Sys);

        var key = DriverKey.Create(1, 99);
        var create = new CreateModelDriverMessage(key, "First", "Last", "TST", "DE", "Team");
        var created = await region.Ask<CreatedDriverMessage>(create, TimeSpan.FromSeconds(3));
        Assert.True(created.IsSuccess);
        Assert.Equal(key, created.Key);

        var echo = await region.Ask<string>(new UpdatePositionMessage(key, 1, DateTime.UtcNow), TimeSpan.FromSeconds(3));
        Assert.Equal(key.ToString(), echo);
    }

    [Fact]
    public async Task ShardRegion_should_return_NotInitialized_if_message_before_create()
    {
        var region = await ShardRegionTools.EnsureRegionStarted(Sys);
        var key = DriverKey.Create(2, 5);
        var reply = await region.Ask<object>(new UpdateTelemetryMessage(key, 123.4, DateTime.UtcNow), TimeSpan.FromSeconds(3));
        Assert.IsType<NotInitializedMessage>(reply);
    }

    [Fact]
    public async Task Entity_should_not_receive_messages_after_passivation()
    {
        var region = await ShardRegionTools.EnsureRegionStarted(Sys);
        var key = DriverKey.Create(4, 44);
        await region.Ask<CreatedDriverMessage>(new CreateModelDriverMessage(key, "A", "B", "C", "DE", "Team"));

        // Ask entity to passivate itself through region protocol
        region.Tell(new TestPassivateMessage(key));

        // wait a tiny bit for passivation to complete
        await Task.Delay(50);

        var reply = await region.Ask<object>(new UpdateTelemetryMessage(key, 123.4, DateTime.UtcNow), TimeSpan.FromSeconds(3));
        Assert.IsType<NotInitializedMessage>(reply);
    }
}