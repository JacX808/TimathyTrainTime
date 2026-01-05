namespace TTT.TrainData.DataSets.Options;

public sealed class NetRailOptions
{
    public string ConnectUrl { get; set; } = "failover:(tcp://publicdatafeeds.networkrail.co.uk:61619" +
                                             "?transport.useInactivityMonitor=false)" +
                                             "?initialReconnectDelay=250" +
                                             "&reconnectDelay=500";
    public string? Username { get; set; }
    public string? Password { get; set; } 

    public List<string> Topics
    {
        get; set;
    } = ["TRAIN_MVT_ALL_TOC", "VSTP_ALL"];
    public bool UseDurableSubscription { get; set; } = true;
    public string ClientId { get; set; } = "ttt-nrod-client-1";

    public string? CorpusUrl { get; set; }
}