using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace RoomBooking.Json;

/// <summary>
/// Reads ISO 8601 timestamps only when they state their offset (<c>Z</c> or <c>±hh:mm</c>).
/// The default converter interprets a missing offset as the server's local time zone, so the same
/// request could be accepted or rejected depending on where the API runs.
/// </summary>
public partial class ExplicitOffsetDateTimeOffsetConverter : JsonConverter<DateTimeOffset>
{
    public const string ErrorMessage =
        "Times must be ISO 8601 with an explicit UTC offset, for example 2030-01-15T10:00:00Z";

    [GeneratedRegex(@"(Z|[+-]\d{2}:\d{2})$", RegexOptions.IgnoreCase)]
    private static partial Regex ExplicitOffset();

    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String
            && ExplicitOffset().IsMatch(reader.GetString()!)
            && reader.TryGetDateTimeOffset(out DateTimeOffset value))
        {
            return value;
        }

        throw new JsonException(ErrorMessage);
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value);
}
