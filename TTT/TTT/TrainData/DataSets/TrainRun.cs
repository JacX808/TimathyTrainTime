namespace TTT.TrainData.DataSets;

public sealed class TrainRun
{
    public string TrainId { get; set; } = default!;
    public DateOnly? ServiceDate { get; set; }
    public string? TrainUid { get; set; }
    public string? TocId { get; set; }
    public DateTimeOffset FirstSeenUtc { get; set; }
    public DateTimeOffset LastSeenUtc { get; set; }
}