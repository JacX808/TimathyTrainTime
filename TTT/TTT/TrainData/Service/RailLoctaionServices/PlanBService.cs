using System.Globalization;
using TTT.TrainData.Records;
using TTT.TrainData.Utility;

namespace TTT.TrainData.Service.RailLoctaionServices;

public class PlanBService : IPlanBService
{
    public async IAsyncEnumerable<BplanLocRow> ReadBplanLocAsync(string path, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        using var fs = File.OpenRead(path);
        using var sr = new StreamReader(fs);

        while (!sr.EndOfStream)
        {
            ct.ThrowIfCancellationRequested();
            var line = await sr.ReadLineAsync(ct);
            if (line is null) break;
            if (line.Length < 3) continue;

            // tab-separated variable-length
            var parts = line.Split('\t');

            if (!parts[0].Equals("LOC", StringComparison.OrdinalIgnoreCase))
                continue;

            // parts[1] is action code; usually 'A'
            if (parts.Length < 11) continue;

            var tiploc = Utility.Converters.NormalizeTiploc(parts[2]);
            var name = parts.ElementAtOrDefault(3)?.Trim();

            var validFrom = ParseBplanDate(parts.ElementAtOrDefault(4));
            var validTo = ParseBplanDate(parts.ElementAtOrDefault(5));

            var easting = ParseInt(parts.ElementAtOrDefault(6));
            var northing = ParseInt(parts.ElementAtOrDefault(7));

            // STANOX field is index 10 per wiki
            var stanoxRaw = parts.ElementAtOrDefault(10)?.Trim();

            yield return new BplanLocRow(tiploc, name, validFrom, validTo, easting, northing, stanoxRaw);
        }
    }

    private static DateTimeOffset? ParseBplanDate(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;

        if (DateTime.TryParseExact(s.Trim(), Constants.BplanDateFormat, Constants.Invariant, DateTimeStyles.AssumeUniversal, out var dt))
            return new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc));

        return null;
    }

    private static int? ParseInt(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        return int.TryParse(s.Trim(), NumberStyles.Integer, Constants.Invariant, out var v) ? v : null;
    }
    
}