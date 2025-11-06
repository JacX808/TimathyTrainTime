namespace TTT.DataSets;

public sealed class MovementEvent
{
    public long Id { get; set; }
    public string TrainId { get; set; } = default!;       // body.train_id
    public string EventType { get; set; } = default!;     // ARRIVAL/DEPARTURE
    public long ActualTimestampMs { get; set; }           // body.actual_timestamp
    public string LocStanox { get; set; } = default!;     // body.loc_stanox
    public string? Platform { get; set; }                 // body.platform
    public string VariationStatus { get; set; } = default!; // ON TIME/EARLY/LATE
    public string? NextReportStanox { get; set; }
    public string TocId { get; set; } = default!;
}
