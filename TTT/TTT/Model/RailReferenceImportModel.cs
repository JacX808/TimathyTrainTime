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
    ICorpusReferenceFileService corpusReferenceFileService, IPlanBService planBService, ICorpusService corpusService,
    ILogger<RailReferenceImportModel> log) : IRailReferenceImportModel
{
    
    public async Task<int> ImportAllRailAsync(CancellationToken cancellationToken)
    {
        var optsValue = options.Value;
        var total = 0;

        try
        {
            total = await ImportRailLocationAsync(cancellationToken);
            log.LogInformation($"Rail reference data imported. Total: {total}");

            var totalLite = await ImportRailLocationLiteAsync(cancellationToken);
            log.LogInformation($"RailLocationLite data created. Total: {totalLite}");

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

    public async Task<bool> CorpusCheckAsync(CancellationToken cancellationToken)
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

    public async Task<List<RailLocationLiteConverted>> GetAllRailLocationLiteAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await database.RailLocations
                .AsNoTracking()
                .Where(railLocation => railLocation.Latitude != null && railLocation.Longitude != null) // optional but usually helpful
                .OrderBy(railLocation => railLocation.Stanox)
                .Select(railLocation => new RailLocationLiteConverted
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
        

    public async Task<RailLocations> GetRailLocationAsync(string stanox, CancellationToken cancellationToken)
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

    public async Task<int> ImportRailLocationAsync(CancellationToken cancellationToken)
    {
        // 1) Load CORPUS (TIPLOC -> STANOX)
        var tiplocToStanox = await corpusService.LoadCorpusTipLocMapAsync(options.Value.CorpusPath, cancellationToken);

        // 2) Parse BPLAN LOC records into RailLocations
        var now = DateTimeOffset.UtcNow;
        var toInsert = new List<RailLocations>(capacity: 50_000);

        await foreach (var loc in planBService.ReadBplanLocAsync(options.Value.BplanPath, cancellationToken))
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

            toInsert.Add(new RailLocations
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
        
        await using var transactionAsync = await database.Database.BeginTransactionAsync(cancellationToken);

        var deleted = await database.RailLocations.ExecuteDeleteAsync(cancellationToken);
        log.LogInformation("RailLocations full refresh: deleted {deleted} rows.", deleted);

        const int batchSize = 5000;
        var total = 0;

        for (var i = 0; i < toInsert.Count; i += batchSize)
        {
            var batch = toInsert.Skip(i).Take(batchSize).ToList();
            await database.RailLocations.AddRangeAsync(batch, cancellationToken);
            total += await database.SaveChangesAsync(cancellationToken);

            // keep change tracker small
            database.ChangeTracker.Clear();
        }

        await transactionAsync.CommitAsync(cancellationToken);

        log.LogInformation("RailLocations full refresh: inserted {count} rows.", total);
        return total;
    }

    public async Task<int> ImportRailLocationLiteAsync(CancellationToken cancellationToken)
    {
        var toInsert = new List<RailLocationLite>(capacity: 50_000);

        foreach (var rail in database.RailLocations)
        {
            toInsert.Add(new RailLocationLite
            {
                Stanox = rail.Stanox,
                Latitude = rail.Latitude,
                Longitude = rail.Longitude
            });
        }
        
        await using var transactionAsync = await database.Database.BeginTransactionAsync(cancellationToken);
        
        var deleted = await database.RailLocationLite.ExecuteDeleteAsync(cancellationToken);
        log.LogInformation("RailLocationsLite full refresh: deleted {deleted} rows.", deleted);
        
        const int batchSize = 5000;
        var total = 0;
        
        try
        {
            for (var i = 0; i < toInsert.Count; i += batchSize)
            {
                var batch = toInsert.Skip(i).Take(batchSize).ToList();
                await database.RailLocationLite.AddRangeAsync(batch, cancellationToken);
                total += await database.SaveChangesAsync(cancellationToken);

                // keep change tracker small
                database.ChangeTracker.Clear();
            }
        }
        catch (DbUpdateException ex)
        {
            log.LogError(ex,
                "DB update failed. Message={Message} Inner={Inner}",
                ex.Message,
                ex.InnerException?.Message);

            throw;
        }
        
        await transactionAsync.CommitAsync(cancellationToken);
        log.LogInformation("RailLocationsLite full refresh: inserted {count} rows.", total);
        
        return total;
    }
}