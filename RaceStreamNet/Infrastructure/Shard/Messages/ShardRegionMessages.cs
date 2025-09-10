using Infrastructure.Shard.Interfaces;
using Infrastructure.Shard.Models;

namespace Infrastructure.Shard.Messages;


// CREATE (Meta)
public sealed record CreateModelDriverMessage : IHasDriverId
{
    public DriverKey Key { get; set; }
    public string FirstName { get; init; }
    public string LastName { get; init; }
    public string Acronym { get; init; } // z.B. VER, HAM
    public string CountryCode { get; init; } // ISO-3166 (z.B. NL, GB)
    public string TeamName { get; init; }

    public CreateModelDriverMessage() {}

    public CreateModelDriverMessage(DriverKey key)
    {
        Key = key;
    }

    public CreateModelDriverMessage(DriverKey key, string firstName, string lastName, string acronym,
        string countryCode, string teamName)
        : this(key)
    {
        Key = key;
        FirstName = firstName;
        LastName = lastName;
        Acronym = acronym;
        CountryCode = countryCode;
        TeamName = teamName;
    }
}

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

public sealed record CreatedDriverMessage : IHasDriverId
{
    public DriverKey Key { get; set; }
    public bool IsSuccess { get; private set; }
    public string ErrorMsg { get; private set; } = string.Empty;

    public CreatedDriverMessage()
    {}

    public CreatedDriverMessage(DriverKey key)
    {
        Key = key;
        IsSuccess = true;
    }

    public CreatedDriverMessage(string errorMsg)
    {
        Key = null!;
        IsSuccess = false;
        ErrorMsg = errorMsg;
    }
}