namespace TTT.OpenRail;

public sealed class NrodOptions
{
    public string ConnectUrl { get; set; } = "activemq:tcp://publicdatafeeds.networkrail.co.uk:61619?transport.useInactivityMonitor=false&initialReconnectDelay=250&reconnectDelay=500&consumerExpiryCheckEnabled=false";
    public string? NR_USERNAME { get; set; }     // will come from env/Secrets
    public string? NR_PASSWORD { get; set; }     // will come from env/Secrets
    public string[] Topics { get; set; } = new[] { "TRAIN_MVT_ALL_TOC", "VSTP_ALL" };
    public bool UseDurableSubscription { get; set; } = true;
}