using System.Text.Json;
using TTT.TrainData.Utility;

namespace TTT.TrainData.Service.RailLoctaionServices;

public class CorpusService : ICorpusService
{
    public async Task<Dictionary<string, string>> LoadCorpusTipLocMapAsync(string path, CancellationToken ct)
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

    }

    static void TryAddCorpus(JsonElement el, Dictionary<string, string> map)
    {
        // Fields per wiki: STANOX, TIPLOC, etc.
        if (!TryGetString(el, "TIPLOC", out var tiploc)) return;
        if (!TryGetString(el, "STANOX", out var stanoxRaw)) return;

        tiploc = Utility.Converters.NormalizeTiploc(tiploc);
        var stanox = Utility.Converters.NormalizeStanox(stanoxRaw);
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
            JsonValueKind.Number => p.TryGetInt32(out var n) ? n.ToString(Constants.Invariant) : "",
            _ => ""
        };

        return !string.IsNullOrWhiteSpace(value);
    }
}
