using Akka.Actor;
using Akka.TestKit.Xunit2;
using DiverShardHost.Actors;
using Infrastructure.Models;
using Infrastructure.Shard.Exceptions;
using Infrastructure.Shard.Messages;
using Infrastructure.Shard.Responses;
using Xunit;

namespace Tests.ShardRegion;

public sealed class DriverActorTests : TestKit
{
    [Fact]
    public void UpdateDriverTelemetry_should_update_state_and_ack()
    {
        // Arrange: EntityId via ActorName simulation (like ShardRegion)
        var entityId = "DRIVER_44";
        var actor = Sys.ActorOf(Props.Create(() => new DriverActor()), entityId);

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
        Assert.Equal(12, resp.State!.LapNumber);
        Assert.Equal(3, resp.State.PositionOnTrack);
        Assert.Equal(TyreCompound.Soft, resp.State.CurrentTyreCompound);
    }

    [Fact]
    public void UpdateDriverTelemetry_with_wrong_id_should_fail_and_keep_state()
    {
        var actor = Sys.ActorOf(Props.Create(() => new DriverActor()), "DRIVER_11");

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
        actor.Tell(new GetDriverState("DRIVER_11"));
        var resp = ExpectMsg<DriverStateResponse>();
        Assert.True(resp.IsSuccess);
        Assert.Equal("DRIVER_11", resp.DriverId);
        Assert.NotNull(resp.State);
        Assert.Equal(0, resp.State!.LapNumber);
    }

    [Fact]
    public void GetDriverState_with_wrong_id_should_return_failure_response()
    {
        var actor = Sys.ActorOf(Props.Create(() => new DriverActor()), "DRIVER_X");

        actor.Tell(new GetDriverState("ANOTHER_ID"));
        var resp = ExpectMsg<DriverStateResponse>(TimeSpan.FromSeconds(2));

        Assert.False(resp.IsSuccess);
        Assert.IsType<DriverInShardNotFoundException>(resp.Error);
    }
}