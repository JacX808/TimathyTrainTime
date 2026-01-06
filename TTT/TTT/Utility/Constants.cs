using System.Globalization;

namespace TTT.Utility;

public abstract class Constants
{
    public static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    public const string BplanDateFormat = "dd-MM-yyyy HH:mm:ss";
}