namespace TTT.TrainData.DataSets;

public sealed class OpenRailOptions
{
    public string Source { get; set; } = "Kafka";
    public string BootstrapServers { get; set; } = default!;
    public string Topic { get; set; } = "TRAIN_MVT_ALL_TOC";
    public string GroupId { get; set; } = "ttt-trust-consumer";
    public string? Username { get; set; }
    public string? Password { get; set; }
    public bool EnableTls { get; set; } = true;
}