using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TTT.Database;
using TTT.DataSets.Options;
using TTT.DataSets.RailLocations;
using TTT.Exceptions;
using TTT.Service.RailLocationServices;
using TTT.Utility;

namespace TTT.Model;

public sealed class RailReferenceImportModel(TttDbContext database, IOptions<RailReferenceImportOptions> options,
    CorpusReferenceFileService corpusReferenceFileService, IPlanBService planBService, ICorpusService corpusService,
    ILogger<RailReferenceImportModel> log) : IRailReferenceImportModel
{
    
    public async Task<int> ImportRailAsync(CancellationToken cancellationToken)
    {
        var optsValue = options.Value;
        var total = 0;

        try
        {
            total = await ImportAsync(optsValue.CorpusPath, optsValue.BplanPath, cancellationToken);
            log.LogInformation($"Rail reference data imported. Total: {total}");
            
        }
        catch (RailReferenceImportException referenceImportException)
        {
            log.LogError(referenceImportException,
                "Error importing rail reference data. CorpusPath={CorpusPath}, BplanPath={BplanPath}",
                optsValue.CorpusPath,
                optsValue.BplanPath);
            return total;
        }

        return total;
    }

    public async Task<bool> RunCorpusCheckAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await corpusReferenceFileService.DownloadAndExtractCorpusAsync(cancellationToken);
            log.LogInformation($"Corpus check complete and stored in {result}");
            return true;
        }
        catch (Exception e)
        {
            log.LogError($"Corpus check failed {e.Message}");
            return false;
        }
    }

    public async Task<List<RailLocationLite>> GetAllRailLocationLiteAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await database.RailLocations
                .AsNoTracking()
                .Where(railLocation => railLocation.Latitude != null && railLocation.Longitude != null) // optional but usually helpful
                .OrderBy(railLocation => railLocation.Stanox)
                .Select(railLocation => new RailLocationLite
                {
                    Stanox = railLocation.Stanox,
                    Latitude = railLocation.Latitude,
                    Longitude = railLocation.Longitude
                })
                .ToListAsync(cancellationToken);
            
            return result;
        }
        catch (Exception e)
        {
            log.LogError(e, "Get all rail location lite failed");
            return null!;
        }
    }
        

    public async Task<RailLocation> GetRailLocationAsync(string stanox, CancellationToken cancellationToken)
    {
        var normalized = Converters.NormalizeStanox(stanox);

        try
        {
            var loc = await database.RailLocations
                .AsNoTracking()
                .Where(railLocation => railLocation.Stanox == normalized)
                .OrderByDescending(railLocation => railLocation.UpdatedAt)
                .ThenBy(railLocation => railLocation.Tiploc)
                .FirstOrDefaultAsync(cancellationToken);

            return loc!;
        }
        catch (Exception e)
        {
            log.LogError(e, $"Get rail location error on {stanox}");
            return null!;
        }
        
    }

    private async Task<int> ImportAsync(string corpusPath, string bplanPath, CancellationToken ct)
    {
        // 1) Load CORPUS (TIPLOC -> STANOX)
        var tiplocToStanox = await corpusService.LoadCorpusTipLocMapAsync(corpusPath, ct);

        // 2) Parse BPLAN LOC records into RailLocations
        var now = DateTimeOffset.UtcNow;
        var toInsert = new List<RailLocation>(capacity: 50_000);

        await foreach (var loc in planBService.ReadBplanLocAsync(bplanPath, ct))
        {
            // Must have coords
            if (loc.OsEasting is null || loc.OsNorthing is null) continue;

            // Resolve STANOX: prefer BPLAN STANOX, else CORPUS by TIPLOC
            var stanox = Converters.NormalizeStanox(loc.StanoxRaw);
            if (stanox is null)
            {
                if (!tiplocToStanox.TryGetValue(loc.Tiploc, out var fromCorpus))
                    continue;

                stanox = fromCorpus;
            }

            var (lat, lon) = Mappers.OsgbToWgs84.Convert(loc.OsEasting.Value, loc.OsNorthing.Value);

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
}