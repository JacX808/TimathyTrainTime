using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TTT.Database;
using TTT.TrainData.DataSets;
using TTT.TrainData.DataSets.Options;
using TTT.TrainData.Exceptions;
using TTT.TrainData.Service;

namespace TTT.TrainData.Model;

public sealed class RailReferenceImportModel(TttDbContext database, IOptions<RailReferenceImportOptions> opts,
    ILogger<RailReferenceImportModel> log) : IRailReferenceImportModel
{
    public async Task<bool> ImportRailAsync(CancellationToken cancellationToken)
    {
        var optsValue = opts.Value;

        try
        {
            log.LogInformation("Auto-importing rail reference data...");
            var total = await ImportAsync(optsValue.CorpusPath, optsValue.BplanPath, cancellationToken);
            log.LogInformation($"Rail reference data imported. Total: {total}");
            
        }
        catch (RailReferenceImportException referenceImportException)
        {
            log.LogError(referenceImportException,
                "Error importing rail reference data. CorpusPath={CorpusPath}, BplanPath={BplanPath}",
                optsValue.CorpusPath,
                optsValue.BplanPath);
            return false;
        }

        return true;
    }
    
    private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
    private const string BplanDateFormat = "dd-MM-yyyy HH:mm:ss";

    private async Task<int> ImportAsync(string corpusPath, string bplanPath, CancellationToken ct)
    {
        // 1) Load CORPUS (TIPLOC -> STANOX)
        var tiplocToStanox = await LoadCorpusTipLocMapAsync(corpusPath, ct);

        // 2) Parse BPLAN LOC records into RailLocations
        var now = DateTimeOffset.UtcNow;
        var toInsert = new List<RailLocation>(capacity: 50_000);

        await foreach (var loc in ReadBplanLocAsync(bplanPath, ct))
        {
            // Must have coords
            if (loc.OsEasting is null || loc.OsNorthing is null) continue;

            // Resolve STANOX: prefer BPLAN STANOX, else CORPUS by TIPLOC
            var stanox = NormalizeStanox(loc.StanoxRaw);
            if (stanox is null)
            {
                if (!tiplocToStanox.TryGetValue(loc.Tiploc, out var fromCorpus))
                    continue;

                stanox = fromCorpus;
            }

            var (lat, lon) = OsgbToWgs84.Convert(loc.OsEasting.Value, loc.OsNorthing.Value);

            toInsert.Add(new RailLocation
            {
                Stanox = stanox,
                Tiploc = loc.Tiploc,
                Name = loc.Name,
                OsEasting = loc.OsEasting,
                OsNorthing = loc.OsNorthing,
                Latitude = lat,
                Longitude = lon,
                ValidFrom = loc.ValidFrom,
                ValidTo = loc.ValidTo,
                Source = "BPLAN+CORPUS",
                UpdatedAt = now
            });
        }

        // 3) Full refresh insert
        await using var tx = await database.Database.BeginTransactionAsync(ct);

        var deleted = await database.RailLocations.ExecuteDeleteAsync(ct);
        log.LogInformation("RailLocations full refresh: deleted {deleted} rows.", deleted);

        const int batchSize = 5000;
        var total = 0;

        for (var i = 0; i < toInsert.Count; i += batchSize)
        {
            var batch = toInsert.Skip(i).Take(batchSize).ToList();
            await database.RailLocations.AddRangeAsync(batch, ct);
            total += await database.SaveChangesAsync(ct);

            // keep change tracker small
            database.ChangeTracker.Clear();
        }

        await tx.CommitAsync(ct);

        log.LogInformation("RailLocations full refresh: inserted {count} rows.", total);
        return total;
    }

    // ---------------- CORPUS ----------------

    private static async Task<Dictionary<string, string>> LoadCorpusTipLocMapAsync(string path, CancellationToken ct)
    {
        // CORPUS is "JSON representation"; in practice it’s usually a JSON array.
        // This loader supports both:
        // - single JSON array file
        // - NDJSON (one JSON object per line)
        var text = await File.ReadAllTextAsync(path, ct);
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        text = text.TrimStart();
        if (text.StartsWith("["))
        {
            using var doc = JsonDocument.Parse(text);
            foreach (var el in doc.RootElement.EnumerateArray())
                TryAddCorpus(el, map);
        }
        else
        {
            using var sr = new StringReader(text);
            string? line;
            while ((line = sr.ReadLine()) is not null)
            {
                line = line.Trim();
                if (line.Length == 0) continue;
                using var doc = JsonDocument.Parse(line);
                TryAddCorpus(doc.RootElement, map);
            }
        }

        return map;

        static void TryAddCorpus(JsonElement el, Dictionary<string, string> map)
        {
            // Fields per wiki: STANOX, TIPLOC, etc.
            if (!TryGetString(el, "TIPLOC", out var tiploc)) return;
            if (!TryGetString(el, "STANOX", out var stanoxRaw)) return;

            tiploc = NormalizeTiploc(tiploc);
            var stanox = NormalizeStanox(stanoxRaw);
            if (stanox is null) return;

            map[tiploc] = stanox;
        }

        static bool TryGetString(JsonElement el, string prop, out string value)
        {
            value = "";
            if (!el.TryGetProperty(prop, out var p)) return false;

            value = p.ValueKind switch
            {
                JsonValueKind.String => p.GetString() ?? "",
                JsonValueKind.Number => p.TryGetInt32(out var n) ? n.ToString(Invariant) : "",
                _ => ""
            };

            return !string.IsNullOrWhiteSpace(value);
        }
    }

    private static string NormalizeTiploc(string tiploc)
        => tiploc.Trim().ToUpperInvariant();

    private static string? NormalizeStanox(string? stanoxRaw)
    {
        if (string.IsNullOrWhiteSpace(stanoxRaw)) return null;
        var s = stanoxRaw.Trim();

        // Some feeds might include non-digits; keep only digits
        s = new string(s.Where(char.IsDigit).ToArray());
        if (s.Length == 0) return null;

        // Preserve leading zeros
        return s.PadLeft(5, '0')[^5..];
    }

    // ---------------- BPLAN LOC ----------------

    private sealed record BplanLocRow(
        string Tiploc,
        string? Name,
        DateTimeOffset? ValidFrom,
        DateTimeOffset? ValidTo,
        int? OsEasting,
        int? OsNorthing,
        string? StanoxRaw);

    private async IAsyncEnumerable<BplanLocRow> ReadBplanLocAsync(string path, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
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

            var tiploc = NormalizeTiploc(parts[2]);
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

        if (DateTime.TryParseExact(s.Trim(), BplanDateFormat, Invariant, DateTimeStyles.AssumeUniversal, out var dt))
            return new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc));

        return null;
    }

    private static int? ParseInt(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;
        return int.TryParse(s.Trim(), NumberStyles.Integer, Invariant, out var v) ? v : null;
    }
    
}
