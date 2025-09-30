using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Infrastructure.Http;

public sealed class LenientDoubleConverter : JsonConverter<double?>
{
    public override double? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        try
        {
            return reader.TokenType switch
            {
                JsonTokenType.Null => null,
                JsonTokenType.Number => reader.GetDouble(),
                JsonTokenType.String => double.TryParse(reader.GetString(),
                    NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var d) ? d : null,
                _ => null
            };
        }
        catch
        {
            return null;
        }
    }

    public override void Write(Utf8JsonWriter writer, double? value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value ?? double.NaN);
}