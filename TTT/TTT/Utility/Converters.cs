using System.Text.Json;
using System.Text.Json.Serialization;

namespace TTT.Utility;

public abstract class Converters
{
    internal static long ToLong(long v) => v;
    internal static long ToLong(string? s) => long.TryParse(s, out var v) ? v : 0L;

    public static long? ToNullableLong(long v) => v;
    internal static long? ToNullableLong(string? s) => long.TryParse(s, out var v) ? v : (long?)null;

    internal static int? ToNullableInt(int v) => v;
    internal static int? ToNullableInt(string? s) => int.TryParse(s, out var v) ? v : (int?)null;

    internal static bool ToBool(bool v) => v;
    internal static bool ToBool(string? s)
        => !string.IsNullOrWhiteSpace(s) &&
           (s.Equals("true", StringComparison.OrdinalIgnoreCase) ||
            s.Equals("1"));
    
    internal static string NormalizeTiploc(string tiploc)
        => tiploc.Trim().ToUpperInvariant();
    
    internal static string? NormalizeStanox(string? stanoxRaw)
    {
        if (string.IsNullOrWhiteSpace(stanoxRaw)) return null;
        var trim = stanoxRaw.Trim();

        // Some feeds might include non-digits; keep only digits
        trim = new string(trim.Where(char.IsDigit).ToArray());
        if (trim.Length == 0) return null;

        // Preserve leading zeros
        return trim.PadLeft(5, '0')[^5..];
    }
    
}

internal sealed class StringOrNumberToLongConverter : JsonConverter<long?>
{
    public override long? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out var n)) return n;
        if (reader.TokenType == JsonTokenType.String && long.TryParse(reader.GetString(), out var s)) return s;
        return null;
    }

    public override void Write(Utf8JsonWriter writer, long? value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value ?? 0);
}

internal sealed class StringOrNumberToIntConverter : JsonConverter<int?>
{
    public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number && reader.TryGetInt32(out var n)) return n;
        if (reader.TokenType == JsonTokenType.String && int.TryParse(reader.GetString(), out var s)) return s;
        return null;
    }

    public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
        => writer.WriteNumberValue(value ?? 0);
}

internal sealed class StringOrBoolToBoolConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        return reader.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.String => Parse(reader.GetString()),
            _ => false
        };

        static bool Parse(string? s)
            => !string.IsNullOrWhiteSpace(s) &&
               (s.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                s.Equals("1"));
    }

    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
        => writer.WriteBooleanValue(value);
}
