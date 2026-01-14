namespace Infrastructure.ShardRegion;

public sealed record DriverStateDto
{
    public required DriverKey Key { get; init; }
    public required string DriverName { get; init; }
    public int LapNumber { get; init; }
    public int PositionOnTrack { get; init; }
    public double Speed { get; init; }

    public double DeltaToLeader { get; init; }

    public int TyreLife { get; init; }
    public TyreCompound CurrentTyreCompound { get; init; }
    public int PitStops { get; init; }

    public TimeSpan? LastLapTime { get; init; }

    public TimeSpan? Sector1Time { get; init; }
    public TimeSpan? Sector2Time { get; init; }
    public TimeSpan? Sector3Time { get; init; }

    public DateTime Timestamp { get; init; }


    public static DriverStateDto Create(DriverInfoState info) => new()
    {
        Key = info.Key!,
        DriverName = info.LastName,
        LapNumber = info.LapNumber,
        PositionOnTrack = info.PositionOnTrack,
        Speed = info.Speed,
        DeltaToLeader = info.DeltaToLeader,
        TyreLife = info.TyreLife,
        CurrentTyreCompound = info.CurrentTyreCompound,
        PitStops = info.PitStopCount,
        LastLapTime = info.LastLapTime,
        Sector1Time = info.Sector1Time,
        Sector2Time = info.Sector2Time,
        Sector3Time = info.Sector3Time,
        Timestamp = info.TimestampUtc
    };

    public override string ToString()
    {
        return $"Nr.{Key.DriverNumber} - {DriverName}: Position {PositionOnTrack}";
    }
}
