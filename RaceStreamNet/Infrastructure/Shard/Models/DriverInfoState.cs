using Akka.DistributedData;
using Infrastructure.Shard.Messages;
using Microsoft.VisualBasic.CompilerServices;

namespace Infrastructure.Shard.Models;

public class DriverInfoState
{
    public DriverKey Key { get; private set; }

    public string FirstName { get; private set; } = string.Empty;
    public string LastName { get; private set; } = string.Empty;
    public string Acronym { get; private set; } = string.Empty;
    public string CountryCode { get; private set; } = string.Empty;
    public string TeamName { get; private set; } = string.Empty;

    // ---- Live ----
    public int LapNumber { get; private set; } = 0;
    public int PositionOnTrack { get; private set; } = 0;
    public double Speed { get; private set; } = 0;
    public double DeltaToLeader { get; private set; } = 0;
    public TyreCompound CurrentTyreCompound { get; private set; } = TyreCompound.Unknown;
    public DateTime TimestampUtc { get; private set; } = DateTime.MinValue;

    public TimeSpan? LastLapTime { get; private set; }
    public TimeSpan? Sector1Time { get; private set; }
    public TimeSpan? Sector2Time { get; private set; }
    public TimeSpan? Sector3Time { get; private set; }

    // ---- Historie ----
    public List<LapRecord> Laps { get; } = new();
    public List<PitStopRecord> PitStops { get; } = new();
    public List<StintRecord> Stints { get; } = new();

    public int PitStopCount => PitStops.Count;

    public int TyreLife
    {
        get
        {
            var stint = CurrentStint();
            if (stint is null) return 0;
            // (aktueller Lap − lap_start) + tyre_age_at_start
            return Math.Max(0, (LapNumber - stint.LapStart) + stint.TyreAgeAtStart);
        }
    }

    public DriverInfoState()
    {
        Key = null!;
    }

    public DriverInfoState(DriverKey? key, string firstName, string lastName, string acronym, string countryCode, string teamName, DriverKey key1)
    {
        ArgumentNullException.ThrowIfNull(key);
        Key = key;
        FirstName = firstName;
        LastName = lastName;
        Acronym = acronym;
        CountryCode = countryCode;
        TeamName = teamName;
    }

    public DriverInfoState(int driverNumber, int sessionKey, string firstName, string lastName, string acronym, string countryCode, string teamName, DriverKey key)
    : this(DriverKey.Create(sessionKey, driverNumber),firstName,lastName, acronym, countryCode, teamName, key)
    {

    }

    public DriverInfoState(CreateModelDriverMessage message, DriverKey key) 
        : this(message.Key, message.FirstName, message.LastName, message.Acronym, message.CountryCode, message.TeamName, key)
    {

    }

    public void Apply(CreateModelDriverMessage m)
    {
        Key = m.Key;
        FirstName = m.FirstName;
        LastName = m.LastName;
        Acronym = m.Acronym;
        CountryCode = m.CountryCode;
        TeamName = m.TeamName;
    }

    public void Apply(UpdateTelemetryMessage m)
    {
        EnsureKey(m.Key);
        Speed = m.Speed;
        TimestampUtc = Max(TimestampUtc, m.TimestampUtc);
    }

    public void Apply(UpdatePositionMessage m)
    {
        EnsureKey(m.Key);
        PositionOnTrack = m.PositionOnTrack;
        TimestampUtc = Max(TimestampUtc, m.TimestampUtc);
    }

    public void Apply(UpdateIntervalMessage m)
    {
        EnsureKey(m.Key);
        if (m.GapToLeaderSeconds is { } d) DeltaToLeader = d;
        TimestampUtc = Max(TimestampUtc, m.TimestampUtc);
    }

    public void Apply(RecordLapMessage m)
    {
        EnsureKey(m.Key);
        // upsert LapRecord
        var existingIdx = Laps.FindIndex(x => x.LapNumber == m.LapNumber);
        var rec = new LapRecord(m.LapNumber, m.LapTime, m.Sector1, m.Sector2, m.Sector3, m.DateStartUtc);
        if (existingIdx >= 0) Laps[existingIdx] = rec; else Laps.Add(rec);

        LapNumber = Math.Max(LapNumber, m.LapNumber);
        LastLapTime = m.LapTime;
        Sector1Time = m.Sector1;
        Sector2Time = m.Sector2;
        Sector3Time = m.Sector3;
        TimestampUtc = Max(TimestampUtc, m.DateStartUtc);
    }

    public void Apply(UpdateStintMessage m)
    {
        EnsureKey(m.Key);
        var cmp = m.Compound;
        
        var last = CurrentStint();
        if (last is not null && last.LapStart == m.LapStart)
        {
            var idx = Stints.Count - 1;
            Stints[idx] = new StintRecord(cmp, m.LapStart, m.LapEnd, m.TyreAgeAtStart);
        }
        else
        {
            if (last is not null && last.LapEnd is null)
            {
                var idx = Stints.Count - 1;
                Stints[idx] = last with { LapEnd = m.LapStart };
            }
            Stints.Add(new StintRecord(cmp, m.LapStart, m.LapEnd, m.TyreAgeAtStart));
        }

        CurrentTyreCompound = cmp;
    }

    public void Apply(RecordPitStopMessage m)
    {
        EnsureKey(m.Key);
        PitStops.Add(new PitStopRecord(m.LapNumber, m.PitDuration, m.TimestampUtc));
        TimestampUtc = Max(TimestampUtc, m.TimestampUtc);
    }

    // ---------- Helpers ----------
    private void EnsureKey(DriverKey? key)
    {
        if (Key is null)
            throw new IncompleteInitialization();
    }

    private StintRecord? CurrentStint() =>
        Stints.Count == 0 ? null : Stints[^1];

    private static DateTime Max(DateTime a, DateTime b) => a >= b ? a : b;

    private static TyreCompound ParseCompound(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return TyreCompound.Unknown;
        return Enum.TryParse<TyreCompound>(s.Trim(), true, out var c) ? c : TyreCompound.Unknown;
    }

    public string ToDriverInfoString() =>
        $"DriverInfo name: {FirstName} {LastName} acr: {Acronym} team: {TeamName} from: {CountryCode}";
}

public sealed record LapRecord(
    int LapNumber,
    TimeSpan LapTime,
    TimeSpan Sector1,
    TimeSpan Sector2,
    TimeSpan Sector3,
    DateTime DateStartUtc);

public sealed record PitStopRecord(
    int LapNumber,
    TimeSpan? PitDuration,
    DateTime TimestampUtc);

public sealed record StintRecord(
    TyreCompound Compound,
    int LapStart,
    int? LapEnd,
    int TyreAgeAtStart);

public sealed record TelemetrySnapshot(
    double Speed,
    DateTime TimestampUtc);