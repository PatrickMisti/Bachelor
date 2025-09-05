using Akka.Actor;
using Akka.Hosting;
using Akka.TestKit.Xunit2;
using DriverShardHost.Actors;
using DriverShardHost.Actors.Messages;
using Infrastructure.Shard.Exceptions;
using Infrastructure.Shard.Messages;
using Infrastructure.Shard.Models;
using Moq;
using Xunit;
using Xunit.Abstractions;

namespace Tests.ShardRegion;

public sealed class DriverActorTests : TestKit
{
    private readonly Mock<IRequiredActor<TelemetryRegionHandler>> _telRegionHandler;
    private static readonly string TestConfig = """
                                                akka.stdout-loglevel = DEBUG
                                                akka.loggers = ["Akka.TestKit.TestEventListener, Akka.TestKit"]
                                                akka.loglevel = DEBUG
                                                """;
    public DriverActorTests(ITestOutputHelper output) : base (TestConfig, output)
    {
        _telRegionHandler = new Mock<IRequiredActor<TelemetryRegionHandler>>();

        var notifyProbe = CreateTestProbe();
        _telRegionHandler.SetupGet(x => x.ActorRef).Returns(notifyProbe.Ref);
    }

    [Fact]
    public void UpdateDriverTelemetry_should_update_state_and_ack()
    {
        // Arrange: EntityId via ActorName simulation (like ShardRegion)
        var entityId = new DriverKey { DriverNumber = 1, SessionId = 1111 };

        // thows error wenn _telRegionHandler.tell in driveractor 
        var actor = Sys.ActorOf(Props.Create(() => new DriverActor(_telRegionHandler.Object)), entityId.ToString());

        
        // Act
        actor.Tell(new CreateModelDriverMessage(entityId)
        {
            CountryCode = "AT",
            Acronym = "HSV",
            TeamName = "Red Bull",
            LastName = "Herbert",
            FirstName = "Max"
        });

        // Assert: Ack
        ExpectMsg<CreatedDriverMessage>(TimeSpan.FromSeconds(2));

        // Query
        actor.Tell(new GetDriverStateMessage(entityId));
        var resp = ExpectMsg<DriverStateMessage>(TimeSpan.FromSeconds(2));

        Assert.True(resp.IsSuccess);
        Assert.Equal(entityId, resp.DriverId);
        Assert.NotNull(resp.State);
        Assert.Equal(0, resp.State!.LapNumber);
        Assert.Equal(0, resp.State.PositionOnTrack);
        Assert.Equal(0, resp.State.TyreLife);
    }

    [Fact]
    public async Task UpdateDriverTelemetry_with_wrong_id_should_fail_and_keep_state()
    {
        var entityId = new DriverKey { DriverNumber = 1, SessionId = 1111 };
        var wrongEntity = new DriverKey { DriverNumber = 2, SessionId = 1111 };
        var actor = Sys.ActorOf(Props.Create(() => new DriverActor(_telRegionHandler.Object)), entityId.ToString());

        // create Driver
        actor.Tell(new CreateModelDriverMessage(entityId)
        {
            CountryCode = "AT",
            Acronym = "HSV",
            TeamName = "Red Bull",
            LastName = "Herbert",
            FirstName = "Max"
        });

        // Update with wrong DriverId
        var wrong = new UpdateStintMessage(wrongEntity, TyreCompound.Soft , 2, 3, 0);

        var failure = await actor.Ask<Status.Failure>(wrong);
        //var failure = ExpectMsg<Status.Failure>(TimeSpan.FromSeconds(2));
        Assert.IsType<DriverInShardNotFoundException>(failure.Cause);

        // State should not change
        var resp = await actor.Ask<DriverStateMessage>(new GetDriverStateMessage(entityId));
        Assert.True(resp.IsSuccess);
        Assert.Equal(entityId, resp.DriverId);
        Assert.NotNull(resp.State);
        Assert.Equal(0, resp.State!.LapNumber);
    }

    [Fact]
    public void GetDriverState_with_wrong_id_should_return_failure_response()
    {
        var entityId = new DriverKey { DriverNumber = 1, SessionId = 1111 };
        var actor = Sys.ActorOf(Props.Create(() => new DriverActor(_telRegionHandler.Object)), "DRIVER_X");

        actor.Tell(new GetDriverStateMessage(entityId));
        var resp = ExpectMsg<NotInitializedMessage>(TimeSpan.FromSeconds(2));

        Assert.IsType<NotInitializedMessage>(resp);
    }
}