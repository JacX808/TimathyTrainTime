using Microsoft.EntityFrameworkCore;
using TTT.Database;
using TTT.DataSets.TrainAndRail;

namespace TTT.Model;

public class TrainAndRailMergeModel(TttDbContext dbContext, ILogger<TrainAndRailMergeModel> log) : ITrainAndRailMergeModel
{
    public async Task<int> MergeTrainAndRailDataAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Load current positions
            var current = await dbContext.CurrentTrainPosition
                .AsNoTracking()
                .ToListAsync(cancellationToken);

            if (current.Count == 0)
            {
                log.LogWarning("MergeTrainAndRailDataAsync: No CurrentTrainPosition rows found. Clearing merge table.");
                await ClearMergeTableAsync(cancellationToken);
                return 0;
            }

            var neededStanox = current
                .Select(p => Utility.Converters.NormalizeStanox(p.LocStanox))
                .Where(s => s is not null)
                .Select(s => s!)
                .Distinct()
                .ToList();

            var rail = await dbContext.RailLocationLite
                .AsNoTracking()
                .Where(r => neededStanox.Contains(r.Stanox))
                .ToListAsync(cancellationToken);
            
            var railByStanox = rail
                .GroupBy(r => r.Stanox)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

            // Merge in-memory
            var merged = new List<TrainAndRailMergeLite>(current.Count);
            var invalidStanoxCount = 0;
            var matchedCount = 0;
            var missingCoordCount = 0;

            foreach (var pos in current)
            {
                var stanox = Utility.Converters.NormalizeStanox(pos.LocStanox);
                if (stanox is null)
                {
                    invalidStanoxCount++;
                    log.LogWarning(
                        "MergeTrainAndRailDataAsync: Skipping TrainId={TrainId} due to invalid LocStanox='{LocStanox}'.",
                        pos.TrainId, pos.LocStanox);
                    continue;
                }

                railByStanox.TryGetValue(stanox, out var railLite);

                if (railLite is null || railLite.Latitude is null || railLite.Longitude is null)
                    missingCoordCount++;
                else
                    matchedCount++;

                merged.Add(new TrainAndRailMergeLite
                {
                    TrainId = pos.TrainId,
                    LocStanox = stanox,
                    ReportedAt = pos.ReportedAt,
                    Direction = pos.Direction,
                    Latitude = railLite?.Latitude,
                    Longitude = railLite?.Longitude
                });
            }
            
            await ClearMergeTableAsync(cancellationToken);

            const int batchSize = 5000;
            for (var i = 0; i < merged.Count; i += batchSize)
            {
                var batch = merged.Skip(i).Take(batchSize).ToList();
                await dbContext.TrainAndRailMergeLite.AddRangeAsync(batch, cancellationToken);
                await dbContext.SaveChangesAsync(cancellationToken);
                dbContext.ChangeTracker.Clear();
            }

            log.LogInformation(
                "MergeTrainAndRailDataAsync complete. CurrentPositions={CurrentCount}, Inserted={Inserted}, MatchedWithCoords={Matched}, MissingCoords={MissingCoords}, InvalidStanoxSkipped={InvalidStanox}.",
                current.Count, merged.Count, matchedCount, missingCoordCount, invalidStanoxCount);

            return merged.Count;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "MergeTrainAndRailDataAsync failed.");
            return 0;
        }
    }

    public Task<List<TrainAndRailMergeLite>> GetAllTrainMapDataAsync(DateTimeOffset? date, CancellationToken cancellationToken)
    {
        var queryable = dbContext.TrainAndRailMergeLite.AsNoTracking();

        if (date is null)
        {
            log.LogInformation("GetAllTrainMapDataAsync: No dates found.");
            return Task.FromResult(new List<TrainAndRailMergeLite>());
        }

        var trainData = queryable.Where(lite => lite.ReportedAt == date).ToList();
        
        return Task.FromResult(trainData);
    }

    private async Task ClearMergeTableAsync(CancellationToken ct)
    {
        try
        {
            await dbContext.TrainAndRailMergeLite.ExecuteDeleteAsync(ct);
        }
        catch (NotSupportedException)
        {
            var all = await dbContext.TrainAndRailMergeLite.ToListAsync(ct);
            dbContext.TrainAndRailMergeLite.RemoveRange(all);
            await dbContext.SaveChangesAsync(ct);
        }
    }
}