using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Infrastructure.Shard.Models;

public class DriverKey
{
    [Range(1, 999)]
    public int DriverNumber { get; init; }

    [Range(1, int.MaxValue)]
    public int SessionId { get; init; }

    // Optional: zusätzliches Kombi-Pattern wie "123_12345"
    private static readonly Regex KeyRegex = new(@"^(?:[1-9]\d{0,2})_(?:\d{4,7})$", RegexOptions.Compiled);

    public override string? ToString() => $"S{SessionId}-D{DriverNumber:D2}";

    public string UnderscoreKey() => $"{DriverNumber}_{SessionId}";

    public static DriverKey? Create(int sessionId, int driverNumber)
    {
        var k = new DriverKey { SessionId = sessionId, DriverNumber = driverNumber };
        try
        {
            Validator.ValidateObject(k, new ValidationContext(k), validateAllProperties: true);
        }
        catch (Exception)
        {
            return null;
        }

        // Falls du zusätzlich das Kombi-Regex erzwingen willst:
        if (!KeyRegex.IsMatch($"{driverNumber}_{sessionId}"))
            return null;

        return k;
    }
}