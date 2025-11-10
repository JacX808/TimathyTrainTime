namespace TTT.TrainData.DataSets;

public sealed class TrainActivationBody
{
    public string Train_id { get; set; } = default!;
    public string TrainUid { get; set; } = default!;
    public string? TocId { get; set; }
    public long OriginDepTimestamp { get; set; } // epoch ms
    public string? TpOriginTimestamp { get; set; } // fallback ISO date
}