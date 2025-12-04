namespace TTT.DataSets;

public sealed class TrainActivationBody
{
    public string TrainId { get; set; } = default!;
    public string TrainUid { get; set; } = default!;
    public string? TocId { get; set; }
    public long OriginDepTimestamp { get; set; } // ms
    public string? TpOriginTimestamp { get; set; } // "yyyy-MM-dd"
}
