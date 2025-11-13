namespace TTT.OpenRail;

public sealed class NetRailOptions
{
    public string ConnectUrl { get; set; } = "\"tcp://publicdatafeeds.networkrail.co.uk:61619?transport.useInactivityMonitor=false\"";
    public string? Username { get; set; }     // will come from env/Secrets
    public string? Password { get; set; }     // will come from env/Secrets
    public string[] Topics { get; set; } = new[] { "TRAIN_MVT_ALL_TOC", "VSTP_ALL" };
    public bool UseDurableSubscription { get; set; } = true;
}