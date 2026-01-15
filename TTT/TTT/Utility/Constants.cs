using System.Globalization;

namespace TTT.Utility;

public abstract class Constants
{
    public static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    public const string BplanDateFormat = "dd-MM-yyyy HH:mm:ss";
    public const int maxIngestMessage = 1000;
    public const int maxIngestSeconds = 20;
    public const string IngestTopic = "TRAIN_MVT_ALL_TOC";
    public const string LastStanoxNotAvailable = "N/A";
    public const int OldDataCutoff = -1;

}