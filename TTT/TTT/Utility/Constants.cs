using System.Globalization;

namespace TTT.Utility;

public abstract class Constants
{
    public static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    public const string BplanDateFormat = "dd-MM-yyyy HH:mm:ss";
    public const int maxIngestMessage = 1000;
    public const int maxIngestSeconds = 20;
    public const string ingestTopic = "TRAIN_MVT_ALL_TOC";
}