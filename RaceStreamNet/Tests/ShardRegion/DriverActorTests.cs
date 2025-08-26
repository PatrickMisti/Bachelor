using Akka.Actor;
using Akka.Hosting;
using Akka.TestKit.Xunit2;
using DiverShardHost.Actors;
using Infrastructure.Models;
using Infrastructure.Shard.Exceptions;
using Infrastructure.Shard.Messages;
using Infrastructure.Shard.Responses;
using Moq;
using Xunit;

namespace Tests.ShardRegion;

public sealed class DriverActorTests : TestKit
{
    private readonly Mock<IRequiredActor<TelemetryRegionHandler>> _telRegionHandler;
    public DriverActorTests()
    {
        _telRegionHandler = new Mock<IRequiredActor<TelemetryRegionHandler>>();

        var notifyProbe = CreateTestProbe();
        _telRegionHandler.SetupGet(x => x.ActorRef).Returns(notifyProbe.Ref);
    }

    [Fact]
    public void UpdateDriverTelemetry_should_update_state_and_ack()
    {
        // Arrange: EntityId via ActorName simulation (like ShardRegion)
        var entityId = "DRIVER_44";
        // thows error wenn _telRegionHandler.tell in driveractor 
        var actor = Sys.ActorOf(Props.Create(() => new DriverActor(_telRegionHandler.Object)), entityId);

        var upsert = new UpdateDriverTelemetry(
            DriverId: entityId,
            LapNumber: 12,
            PositionOnTrack: 3,
            Speed: 278.5,
            DeltaToLeader: 1.234,
            TyreLife: 10,
            CurrentTyreCompound: TyreCompound.Soft,
            PitStops: 1,
            LastLapTime: TimeSpan.FromSeconds(92.345),
            Sector1Time: TimeSpan.FromSeconds(29.1),
            Sector2Time: TimeSpan.FromSeconds(31.2),
            Sector3Time: TimeSpan.FromSeconds(32.0),
            Timestamp: DateTime.UtcNow);

        // Act
        actor.Tell(upsert);

        // Assert: Ack
        ExpectMsg<Status.Success>(TimeSpan.FromSeconds(2));

        // Query
        actor.Tell(new GetDriverState(entityId));
        var resp = ExpectMsg<DriverStateResponse>(TimeSpan.FromSeconds(2));

        Assert.True(resp.IsSuccess);
        Assert.Equal(entityId, resp.DriverId);
        Assert.NotNull(resp.State);
        Assert.Equal(upsert.LapNumber, resp.State!.LapNumber);
        Assert.Equal(upsert.PositionOnTrack, resp.State.PositionOnTrack);
        Assert.Equal(upsert.CurrentTyreCompound, resp.State.CurrentTyreCompound);
    }

    [Fact]
    public void UpdateDriverTelemetry_with_wrong_id_should_fail_and_keep_state()
    {
        var entityId = "DRIVER_11";
        var actor = Sys.ActorOf(Props.Create(() => new DriverActor(_telRegionHandler.Object)), entityId);

        // Update with wrong DriverId
        var wrong = new UpdateDriverTelemetry(
            DriverId: "DRIVER_12",
            LapNumber: 1,
            PositionOnTrack: 10,
            Speed: 200,
            DeltaToLeader: 10,
            TyreLife: 20,
            CurrentTyreCompound: TyreCompound.Medium,
            PitStops: 0,
            LastLapTime: null,
            Sector1Time: null,
            Sector2Time: null,
            Sector3Time: null,
            Timestamp: DateTime.UtcNow);

        actor.Tell(wrong);
        var failure = ExpectMsg<Status.Failure>(TimeSpan.FromSeconds(2));
        Assert.IsType<DriverInShardNotFoundException>(failure.Cause);

        // State should not change
        actor.Tell(new GetDriverState(entityId));
        var resp = ExpectMsg<DriverStateResponse>();
        Assert.True(resp.IsSuccess);
        Assert.Equal(entityId, resp.DriverId);
        Assert.NotNull(resp.State);
        Assert.Equal(0, resp.State!.LapNumber);
    }

    [Fact]
    public void GetDriverState_with_wrong_id_should_return_failure_response()
    {
        var actor = Sys.ActorOf(Props.Create(() => new DriverActor(_telRegionHandler.Object)), "DRIVER_X");

        actor.Tell(new GetDriverState("ANOTHER_ID"));
        var resp = ExpectMsg<DriverStateResponse>(TimeSpan.FromSeconds(2));

        Assert.False(resp.IsSuccess);
        Assert.IsType<DriverInShardNotFoundException>(resp.Error);
    }
}