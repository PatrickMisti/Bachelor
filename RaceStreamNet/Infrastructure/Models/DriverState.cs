using Infrastructure.Shard.Messages;

namespace Infrastructure.Models;

public sealed class DriverState
{
    public int LapNumber { get; private set; }
    public int PositionOnTrack { get; private set; }
    public double Speed { get; private set; }
    public double DeltaToLeader { get; private set; }
    public int TyreLife { get; private set; }
    public TyreCompound CurrentTyreCompound { get; private set; }
    public int PitStops { get; private set; }
    public TimeSpan? LastLapTime { get; private set; }
    public TimeSpan? Sector1Time { get; private set; }
    public TimeSpan? Sector2Time { get; private set; }
    public TimeSpan? Sector3Time { get; private set; }
    public DateTime Timestamp { get; private set; }

    public DriverState Apply(UpdateDriverTelemetry m)
    {
        //DriverId = m.DriverId;
        LapNumber = m.LapNumber;
        PositionOnTrack = m.PositionOnTrack;
        Speed = m.Speed;
        DeltaToLeader = m.DeltaToLeader;
        TyreLife = m.TyreLife;
        CurrentTyreCompound = m.CurrentTyreCompound;
        PitStops = m.PitStops;
        LastLapTime = m.LastLapTime;
        Sector1Time = m.Sector1Time;
        Sector2Time = m.Sector2Time;
        Sector3Time = m.Sector3Time;
        Timestamp = m.Timestamp;
        return this;
    }
}