using System.Text.Json;
using System.Text.Json.Serialization;

namespace TTT.TrainData.DataSets;

public sealed class TrainMovementBody
{
    [JsonPropertyName("train_id")]
    public string TrainId { get; set; } = default!;

    [JsonPropertyName("actual_timestamp")]
    [JsonConverter(typeof(StringToLongConverter))]
    public long ActualTimestamp { get; set; }

    [JsonPropertyName("loc_stanox")]
    public string LocStanox { get; set; } = default!;

    [JsonPropertyName("gbtt_timestamp")]
    [JsonConverter(typeof(StringToNullableLongConverter))]
    public long? GbttTimestamp { get; set; }

    [JsonPropertyName("planned_timestamp")]
    [JsonConverter(typeof(StringToLongConverter))]
    public long PlannedTimestamp { get; set; }

    [JsonPropertyName("planned_event_type")]
    public string PlannedEventType { get; set; } = default!; // ARRIVAL/DEPARTURE

    [JsonPropertyName("event_type")]
    public string EventType { get; set; } = default!;        // ARRIVAL/DEPARTURE/PASS

    [JsonPropertyName("event_source")]
    public string EventSource { get; set; } = default!;      // e.g., AUTOMATIC

    [JsonPropertyName("correction_ind")]
    [JsonConverter(typeof(StringToBoolConverter))]
    public bool CorrectionInd { get; set; }

    [JsonPropertyName("offroute_ind")]
    [JsonConverter(typeof(StringToBoolConverter))]
    public bool OffrouteInd { get; set; }

    [JsonPropertyName("direction_ind")]
    public string? DirectionInd { get; set; }                // UP/DOWN

    [JsonPropertyName("platform")]
    public string? Platform { get; set; }                    // keep raw (may contain spaces)

    [JsonPropertyName("route")]
    [JsonConverter(typeof(StringToNullableIntConverter))]
    public int? Route { get; set; }

    [JsonPropertyName("train_service_code")]
    public string? TrainServiceCode { get; set; }

    [JsonPropertyName("division_code")]
    public string? DivisionCode { get; set; }

    [JsonPropertyName("toc_id")]
    public string? TocId { get; set; }

    [JsonPropertyName("timetable_variation")]
    [JsonConverter(typeof(StringToNullableIntConverter))]
    public int? TimetableVariation { get; set; }

    [JsonPropertyName("variation_status")]
    public string? VariationStatus { get; set; }             // ON TIME/EARLY/LATE/OFF ROUTE

    [JsonPropertyName("next_report_stanox")]
    public string? NextReportStanox { get; set; }

    [JsonPropertyName("next_report_run_time")]
    [JsonConverter(typeof(StringToNullableIntConverter))]
    public int? NextReportRunTime { get; set; }

    [JsonPropertyName("train_terminated")]
    [JsonConverter(typeof(StringToBoolConverter))]
    public bool TrainTerminated { get; set; }

    [JsonPropertyName("delay_monitoring_point")]
    [JsonConverter(typeof(StringToBoolConverter))]
    public bool DelayMonitoringPoint { get; set; }

    [JsonPropertyName("reporting_stanox")]
    public string? ReportingStanox { get; set; }

    [JsonPropertyName("auto_expected")]
    [JsonConverter(typeof(StringToBoolConverter))]
    public bool AutoExpected { get; set; }
}

/* ---------------- Converters ---------------- */

file sealed class StringToLongConverter : JsonConverter<long>
{
    public override long Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o)
        => r.TokenType switch
        {
            JsonTokenType.Number => r.GetInt64(),
            JsonTokenType.String => long.TryParse(r.GetString(), out var v) ? v : 0L,
            _ => 0L
        };
    public override void Write(Utf8JsonWriter w, long value, JsonSerializerOptions o) => w.WriteNumberValue(value);
}

file sealed class StringToNullableLongConverter : JsonConverter<long?>
{
    public override long? Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o)
        => r.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.Number => r.GetInt64(),
            JsonTokenType.String => long.TryParse(r.GetString(), out var v) ? v : (long?)null,
            _ => null
        };
    public override void Write(Utf8JsonWriter w, long? value, JsonSerializerOptions o)
    {
        if (value is null) w.WriteNullValue(); else w.WriteNumberValue(value.Value);
    }
}

file sealed class StringToNullableIntConverter : JsonConverter<int?>
{
    public override int? Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o)
        => r.TokenType switch
        {
            JsonTokenType.Null => null,
            JsonTokenType.Number => r.GetInt32(),
            JsonTokenType.String => int.TryParse(r.GetString(), out var v) ? v : (int?)null,
            _ => null
        };
    public override void Write(Utf8JsonWriter w, int? value, JsonSerializerOptions o)
    {
        if (value is null) w.WriteNullValue(); else w.WriteNumberValue(value.Value);
    }
}

file sealed class StringToBoolConverter : JsonConverter<bool>
{
    public override bool Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o)
        => r.TokenType switch
        {
            JsonTokenType.True => true,
            JsonTokenType.False => false,
            JsonTokenType.String => Parse(r.GetString()),
            _ => false
        };

    public override void Write(Utf8JsonWriter w, bool value, JsonSerializerOptions o) => w.WriteBooleanValue(value);

    private static bool Parse(string? s)
        => !string.IsNullOrWhiteSpace(s) &&
           (s.Equals("true", StringComparison.OrdinalIgnoreCase) || s == "1");
}