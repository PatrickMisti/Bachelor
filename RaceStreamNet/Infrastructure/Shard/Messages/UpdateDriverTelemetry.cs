using Infrastructure.Models;
using Infrastructure.Shard.Interfaces;

namespace Infrastructure.Shard.Messages;

public sealed record UpdateDriverTelemetry(
    string DriverId,
    int LapNumber,
    int PositionOnTrack,
    double Speed,
    double DeltaToLeader,
    int TyreLife,
    TyreCompound CurrentTyreCompound,
    int PitStops,
    TimeSpan? LastLapTime,
    TimeSpan? Sector1Time,
    TimeSpan? Sector2Time,
    TimeSpan? Sector3Time,
    DateTime Timestamp
) : IHasDriverId;