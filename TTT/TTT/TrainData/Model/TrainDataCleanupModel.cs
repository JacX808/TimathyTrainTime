using Microsoft.EntityFrameworkCore;
using TTT.Database;

namespace TTT.TrainData.Model;

public class TrainDataCleanupModel(ILogger<TrainDataCleanupModel> log, TttDbContext dbContext) : ITrainDataCleanupModel
{
    public async Task<int> DeleteAllOldTrainPositions(int dayOffset, CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow - TimeSpan.FromDays(dayOffset);

        try
        {
            var deleted = await dbContext.CurrentTrainPosition
                .Where(p => p.ReportedAt < cutoff)
                .ExecuteDeleteAsync(cancellationToken);

            if (deleted > 0)
                log.LogInformation("Deleted {count} old CurrentTrainPosition rows older than {cutoff}.", deleted, cutoff);

            return deleted;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Failed deleting old CurrentTrainPosition rows.");
            return 0;
        }
    }

    public async Task<int> DeleteAllOldMovementEvents(int dateOffset, CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow - TimeSpan.FromDays(dateOffset);
        var cutoffMs = cutoff.ToUnixTimeMilliseconds();

        try
        {
            var deleted = await dbContext.MovementEvents
                .Where(e => e.ActualTimestampMs < cutoffMs)
                .ExecuteDeleteAsync(cancellationToken);

            if (deleted > 0)
                log.LogInformation("Deleted {count} old MovementEvent rows older than {cutoff} ({cutoffMs}).",
                    deleted, cutoff, cutoffMs);

            return deleted;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Failed deleting old MovementEvent rows.");
            return 0;
        }
    }

    public async Task<int> DeleteAllOldTrains(int dateOffest, CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow - TimeSpan.FromDays(dateOffest);

        try
        {
            var runsDeleted = await dbContext.TrainRuns
                .Where(r => r.LastSeenUtc < cutoff)
                .ExecuteDeleteAsync(cancellationToken);

            if (runsDeleted > 0)
                log.LogInformation(
                    "Deleted old trains before {cutoffDate}: TrainRuns={runs}.", cutoff, runsDeleted);

            return runsDeleted;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Failed deleting old TrainRuns (and dependent rows).");
            return 0;
        }
    }

    public async Task<int> DeleteAllMovementData(int dataOffest, CancellationToken cancellationToken)
    {
        int totalDeleted = 0;
        totalDeleted += await DeleteAllOldTrains(dataOffest, cancellationToken);
        totalDeleted += await DeleteAllOldMovementEvents(dataOffest, cancellationToken);
        totalDeleted += await DeleteAllOldTrainPositions(dataOffest, cancellationToken);

        return totalDeleted;
    }
}