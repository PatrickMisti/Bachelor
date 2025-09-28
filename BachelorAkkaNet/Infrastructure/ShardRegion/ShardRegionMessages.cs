using System.ComponentModel.DataAnnotations;

namespace Infrastructure.ShardRegion;

public interface IHasDriverId
{
    DriverKey Key { get; }
}

public readonly record struct DriverKey(int SessionId, int DriverNumber)
{
    public override string ToString() => $"S{SessionId}-D{DriverNumber:D2}";
    public string UnderscoreKey() => $"{DriverNumber}_{SessionId}";

    public static DriverKey Create(int sessionId, int driverNumber)
    {
        // gleiche Regeln wie deine DataAnnotations
        if (sessionId < 1) throw new ArgumentOutOfRangeException(nameof(sessionId));
        if (driverNumber is < 1 or > 999) throw new ArgumentOutOfRangeException(nameof(driverNumber));
        return new DriverKey(sessionId, driverNumber);
    }
}

public sealed record CreateModelDriverMessage(
    DriverKey Key,
    string FirstName,
    string LastName,
    string Acronym,     // z.B. VER, HAM
    string CountryCode, // ISO-3166: NL, GB, …
    string TeamName
) : IHasDriverId;

// LIVE-Updates (small & often)
public sealed record UpdateTelemetryMessage(DriverKey Key, double Speed, DateTime TimestampUtc) : IHasDriverId;
public sealed record UpdatePositionMessage(DriverKey Key, int PositionOnTrack, DateTime TimestampUtc) : IHasDriverId;
public sealed record UpdateIntervalMessage(DriverKey Key, double? GapToLeaderSeconds, DateTime TimestampUtc) : IHasDriverId;

// round (p round)
public record RecordLapMessage(
    DriverKey Key,
    int LapNumber,
    TimeSpan LapTime,
    TimeSpan Sector1,
    TimeSpan Sector2,
    TimeSpan Sector3,
    DateTime DateStartUtc) : IHasDriverId;


// Tyres/Stints
public sealed record UpdateStintMessage(
    DriverKey Key,
    TyreCompound Compound,   // „SOFT“, „MEDIUM“, …
    int LapStart,
    int? LapEnd,
    int TyreAgeAtStart) : IHasDriverId;

// PitStops
public sealed record RecordPitStopMessage(
    DriverKey Key,
    int LapNumber,
    TimeSpan? PitDuration,
    DateTime TimestampUtc) : IHasDriverId;

// Get driver data
public sealed record GetDriverStateMessage(DriverKey Key) : IHasDriverId;

public sealed record StopEntity;

public sealed record NotInitializedMessage(string EntityId);

public sealed record CreatedDriverMessage(DriverKey Key, bool IsSuccess, string? ErrorMsg) : IHasDriverId
{
    public static CreatedDriverMessage Success(DriverKey key) => new(key, true, null);
    public static CreatedDriverMessage Failure(string error) => new(default, false, error);
}

public enum TyreCompound
{
    Soft,
    Medium,
    Hard,
    Wet,
    Intermediate,
    Unknown
}

public static class DriverMessageExtensions
{
    public static DriverInfoState CopyState(this DriverInfoState old) =>
    new(
        key:                 old.Key,
        firstName:           old.FirstName,
        lastName:            old.LastName,
        acronym:             old.Acronym,
        countryCode:         old.CountryCode,
        teamName:            old.TeamName,
        lapNumber:           old.LapNumber,
        positionOnTrack:     old.PositionOnTrack,
        speed:               old.Speed,
        deltaToLeader:       old.DeltaToLeader,
        currentTyreCompound: old.CurrentTyreCompound,
        timestampUtc:        old.TimestampUtc,
        lastLapTime:         old.LastLapTime,
        sector1Time:         old.Sector1Time,
        sector2Time:         old.Sector2Time,
        sector3Time:         old.Sector3Time,
        laps:                [.. old.Laps],
        pitStops:            [..old.PitStops],
        stints:              [.. old.Stints]);
}