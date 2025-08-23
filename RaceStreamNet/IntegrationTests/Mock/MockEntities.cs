using Infrastructure.Models;
using Infrastructure.Shard.Messages;

namespace IntegrationTests.Mock;

public static class MockEntities
{
    public static UpdateDriverTelemetry MOCK_UPDATE_DRIVER_TELEMETRY => new UpdateDriverTelemetry(
        DriverId: "VER",
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

}

