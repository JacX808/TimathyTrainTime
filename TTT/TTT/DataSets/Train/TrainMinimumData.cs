namespace TTT.DataSets.Train;

public sealed class TrainMinimumData
{
    public string TrainId { get; set; } = default!;
    public string LocStanox { get; set; } = default!;
    public string NextLocStanox { get; set; } = default!;
    public DateTimeOffset LastSeenUtc { get; set; }
    public string? VariationStatus { get; set; }
}