namespace TTT.DataSets;

public sealed class CurrentTrainPosition
{
    public string TrainId { get; set; } = default!;
    public string LocStanox { get; set; } = default!;
    public DateTimeOffset ReportedAt { get; set; }
    public string? Direction { get; set; }
    public string? Line { get; set; }
    public string? VariationStatus { get; set; }
}