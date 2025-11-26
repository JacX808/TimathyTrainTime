using System.Text.Json.Serialization;
using TTT.Utility.Converters;

namespace TTT.DataSets;

public sealed class TrainMovementBody
    {
        [JsonPropertyName("train_id")] public string TrainId { get; set; } = default!;

        [JsonPropertyName("actual_timestamp")]
        [JsonConverter(typeof(StringOrNumberToLongConverter))]
        public long ActualTimestamp { get; set; }

        [JsonPropertyName("loc_stanox")] public string LocStanox { get; set; } = default!;

        [JsonPropertyName("gbtt_timestamp")]
        [JsonConverter(typeof(StringOrNumberToLongConverter))]
        public long? GbttTimestamp { get; set; }

        [JsonPropertyName("planned_timestamp")]
        [JsonConverter(typeof(StringOrNumberToLongConverter))]
        public long PlannedTimestamp { get; set; }

        [JsonPropertyName("planned_event_type")]
        public string PlannedEventType { get; set; } = default!; // e.g. ARRIVAL/DEPARTURE

        [JsonPropertyName("event_type")] public string EventType { get; set; } = default!; // ARRIVAL/DEPARTURE/PASS

        [JsonPropertyName("event_source")] public string EventSource { get; set; } = default!; // e.g. AUTOMATIC

        [JsonPropertyName("correction_ind")]
        [JsonConverter(typeof(StringOrBoolToBoolConverter))]
        public bool CorrectionInd { get; set; }

        [JsonPropertyName("offroute_ind")]
        [JsonConverter(typeof(StringOrBoolToBoolConverter))]
        public bool OffrouteInd { get; set; }

        [JsonPropertyName("direction_ind")] public string? DirectionInd { get; set; } // e.g. UP/DOWN

        [JsonPropertyName("platform")] public string? Platform { get; set; } // keep as string; trim if you like

        [JsonPropertyName("route")]
        [JsonConverter(typeof(StringOrNumberToIntConverter))]
        public int? Route { get; set; }

        [JsonPropertyName("train_service_code")]
        public string? TrainServiceCode { get; set; }

        [JsonPropertyName("division_code")] public string? DivisionCode { get; set; }

        [JsonPropertyName("toc_id")] public string? TocId { get; set; }

        [JsonPropertyName("timetable_variation")]
        [JsonConverter(typeof(StringOrNumberToIntConverter))]
        public int? TimetableVariation { get; set; }

        [JsonPropertyName("variation_status")]
        public string? VariationStatus { get; set; } // ON TIME/EARLY/LATE/OFF ROUTE

        [JsonPropertyName("next_report_stanox")]
        public string? NextReportStanox { get; set; }

        [JsonPropertyName("next_report_run_time")]
        [JsonConverter(typeof(StringOrNumberToIntConverter))]
        public int? NextReportRunTime { get; set; }

        [JsonPropertyName("train_terminated")]
        [JsonConverter(typeof(StringOrBoolToBoolConverter))]
        public bool TrainTerminated { get; set; }

        [JsonPropertyName("delay_monitoring_point")]
        [JsonConverter(typeof(StringOrBoolToBoolConverter))]
        public bool DelayMonitoringPoint { get; set; }

        [JsonPropertyName("reporting_stanox")] public string? ReportingStanox { get; set; }

        [JsonPropertyName("auto_expected")]
        [JsonConverter(typeof(StringOrBoolToBoolConverter))]
        public bool AutoExpected { get; set; }
    }