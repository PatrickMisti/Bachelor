namespace Infrastructure.General;

public sealed class DriverData
{
    public required string DriverId { get; set; }
    public int LapNumber { get; set; }
    public int PositionOnTrack { get; set; }
    public double Speed { get; set; }
    public double DeltaToLeader { get; set; }

    public int TyreLife { get; set; }
    public TyreCompound CurrentTyreCompound { get; set; }
    public int PitStops { get; set; }

    public TimeSpan? LastLapTime { get; set; }
    public TimeSpan? Sector1Time { get; set; }
    public TimeSpan? Sector2Time { get; set; }
    public TimeSpan? Sector3Time { get; set; }

    public DateTime Timestamp { get; set; }

    // Optional
    public bool IsInPit { get; set; }

}