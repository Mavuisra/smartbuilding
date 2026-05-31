using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartBuilding.Infrastructure.Sync;

/// <summary>
/// Dates ISO 8601 en chaîne pour l'API Django (champs CharField updatedAt).
/// </summary>
internal sealed class SyncDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            var text = reader.GetString();
            if (!string.IsNullOrWhiteSpace(text)
                && DateTime.TryParse(text, null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt))
                return dt;
        }

        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out var ticks))
            return new DateTime(ticks, DateTimeKind.Utc);

        return DateTime.MinValue;
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };
        writer.WriteStringValue(utc.ToString("o"));
    }
}

internal sealed class SyncNullableDateTimeJsonConverter : JsonConverter<DateTime?>
{
    private readonly SyncDateTimeJsonConverter _inner = new();

    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;
        return _inner.Read(ref reader, typeof(DateTime), options);
    }

    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (!value.HasValue)
        {
            writer.WriteNullValue();
            return;
        }

        _inner.Write(writer, value.Value, options);
    }
}
