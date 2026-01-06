namespace TTT.DataSets;

public sealed class MovementEvent
{
    public long Id { get; set; }

    // Core identifiers
    public string TrainId { get; set; } = default!;
    public string LocStanox { get; set; } = default!;

    // Timestamps (epoch ms)
    public long ActualTimestampMs { get; set; }           // actual_timestamp
    public long? GbttTimestampMs { get; set; }            // gbtt_timestamp (nullable)
    public long PlannedTimestampMs { get; set; }          // planned_timestamp

    // Event details
    public string PlannedEventType { get; set; } = default!;  // planned_event_type (ARRIVAL/DEPARTURE)
    public string EventType { get; set; } = default!;         // event_type (ARRIVAL/DEPARTURE/PASS)
    public string EventSource { get; set; } = default!;       // event_source (e.g., AUTOMATIC)

    // Indicators / flags
    public bool CorrectionInd { get; set; }               // correction_ind
    public bool OffrouteInd { get; set; }                 // offroute_ind
    public bool TrainTerminated { get; set; }             // train_terminated
    public bool DelayMonitoringPoint { get; set; }        // delay_monitoring_point
    public bool AutoExpected { get; set; }                // auto_expected

    // Direction / platform / line / route
    public string? DirectionInd { get; set; }             // direction_ind (e.g., UP/DOWN)
    public string? Platform { get; set; }                 // platform (string; may contain spaces)
    public string? Line { get; set; }                     // line_ind (if you capture it elsewhere)
    public int? Route { get; set; }                       // route (numeric code)

    // Codes
    public string? TrainServiceCode { get; set; }         // train_service_code
    public string? DivisionCode { get; set; }             // division_code
    public string? TocId { get; set; }                    // toc_id

    // Variation / performance
    public int? TimetableVariation { get; set; }          // timetable_variation
    public string? VariationStatus { get; set; }          // variation_status (ON TIME/EARLY/LATE/OFF ROUTE)

    // Next/Reporting locations
    public string? NextReportStanox { get; set; }         // next_report_stanox
    public int? NextReportRunTime { get; set; }           // next_report_run_time (seconds/mins per feed)
    public string? ReportingStanox { get; set; }          // reporting_stanox
}