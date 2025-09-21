using System.Text.Json.Serialization;
using Infrastructure.ShardRegion;

namespace Infrastructure.Http;

public interface IOpenF1Dto;

public sealed record IntervalDriverDto(
    [property: JsonPropertyName(name: "session_key")] int SessionKey,
    [property: JsonPropertyName(name: "driver_number")] int DriverNumber,
    [property: JsonPropertyName(name: "interval")] double? Interval,
    [property: JsonPropertyName(name: "gap_to_leader")] double? GapToLeader,
    [property: JsonPropertyName(name: "date")] DateTimeOffset CurrentDateTime) : IOpenF1Dto;

public sealed record PersonalDriverDataDto(
    [property: JsonPropertyName(name: "session_key")] int SessionKey,
    [property: JsonPropertyName(name: "driver_number")] int DriverNumber,
    [property: JsonPropertyName(name: "first_name")] string FirstName,
    [property: JsonPropertyName("last_name")] string LastName,
    [property: JsonPropertyName("country_code")] string CountryCode,
    [property: JsonPropertyName("team_name")] string TeamName,
    [property: JsonPropertyName(name: "name_acronym")] string Acronym) : IOpenF1Dto;

public sealed record PositionOnTrackDto(
    [property: JsonPropertyName(name: "session_key")] int SessionKey,
    [property: JsonPropertyName(name: "driver_number")] int DriverNumber,
    [property: JsonPropertyName(name: "position")] int PositionOnTrack,
    [property: JsonPropertyName("date")] DateTimeOffset CurrentDateTime) : IOpenF1Dto;

public sealed record TelemetryDateDto(
    [property: JsonPropertyName(name: "session_key")] int SessionKey,
    [property: JsonPropertyName(name: "driver_number")] int DriverNumber,
    [property: JsonPropertyName(name: "n_gear")] int Gear,
    [property: JsonPropertyName(name: "speed")] int Speed,
    [property: JsonPropertyName(name: "drs")] int Drs,
    [property: JsonPropertyName(name: "throttle")] int Throttle,
    [property: JsonPropertyName(name: "brake")] int Brake,
    [property: JsonPropertyName(name: "rpm")] int Rpm,
    [property: JsonPropertyName("date")] DateTimeOffset CurrentDateTime) : IOpenF1Dto;



public static class OpenF1DtoExtensions
{
    public static CreateModelDriverMessage ToMap(this PersonalDriverDataDto dto) =>
        new(
            DriverKey.Create(dto.SessionKey, dto.DriverNumber),
            dto.FirstName,
            dto.LastName,
            dto.Acronym,
            dto.CountryCode,
            dto.TeamName);

    public static UpdateIntervalMessage ToMap(this IntervalDriverDto dto) =>
        new(
            DriverKey.Create(dto.SessionKey, dto.DriverNumber),
            dto.GapToLeader,
            dto.CurrentDateTime.UtcDateTime);

    public static UpdatePositionMessage ToMap(this PositionOnTrackDto dto) =>
        new(DriverKey.Create(dto.SessionKey, dto.DriverNumber),
            dto.PositionOnTrack,
            dto.CurrentDateTime.UtcDateTime);

    public static UpdateTelemetryMessage ToMap(this TelemetryDateDto dto) =>
        new(DriverKey.Create(dto.SessionKey, dto.DriverNumber),
            dto.Speed,
            dto.CurrentDateTime.UtcDateTime);
}