// File: Services/TrainDataService.cs

using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TTT.Database;
using TTT.TrainData.DataSets;

namespace TTT.TrainData.Model;

public sealed class TrainDataModel(TttDbContext dbContext, ILogger<TrainDataModel> log) : ITrainDataModel
{
    
    public Task<TrainRun?> FindTrainRunAsync(string trainId, CancellationToken cancellationToken)
        => dbContext.TrainRuns.AsNoTracking().SingleOrDefaultAsync(trainRun => trainRun.TrainId == trainId, cancellationToken);

    public async Task AddTrainRunAsync(TrainRun run, CancellationToken ct)
        => await dbContext.TrainRuns.AddAsync(run, ct);

    public async Task<bool> AddMovementEventAsync(MovementEvent movementEvent, CancellationToken cancellationToken, bool ignoreDuplicates = true)
    {
        if (!ignoreDuplicates) 
            return true;
        
        try
        {
            await dbContext.MovementEvents.AddAsync(movementEvent, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken); // persist early timeOffset surface dup quickly
            return true;
        }
        catch (DbUpdateException dbUpdateException)
        {
            // Likely unique index violation on (train_id, ActualTimestampMs, loc_stanox, event_type)
            dbContext.Entry(movementEvent).State = EntityState.Detached;
            log.LogDebug(dbUpdateException, "Duplicate MovementEvent ignored for {train_id}@{Ts}/{Loc}/{Type}",
                movementEvent.TrainId, movementEvent.ActualTimestampMs, movementEvent.LocStanox, movementEvent.EventType);
            return false;
        }
    }

    public async Task<CurrentTrainPosition?> GetPosition(string trainId, CancellationToken cancellationToken)
    {
        var pos = 
            await dbContext.CurrentTrainPosition.SingleOrDefaultAsync(currentTrainPosition => 
                currentTrainPosition.TrainId == trainId, cancellationToken);
        
        return pos;
    }

    public async Task<List<MovementEvent>> GetMovements(string trainId, DateTimeOffset? from,
        DateTimeOffset? timeOffset,
        CancellationToken cancellationToken)
    {
        var queryable = dbContext.MovementEvents.AsNoTracking().Where(x => x.TrainId == trainId);
        if (from is not null) 
            queryable = queryable.Where(x => x.ActualTimestampMs >= from.Value.ToUnixTimeMilliseconds());
        
        if (timeOffset   is not null) 
            queryable = queryable.Where(x => x.ActualTimestampMs <= timeOffset.Value.ToUnixTimeMilliseconds());
        
        var list = await queryable.OrderBy(x => x.ActualTimestampMs).ToListAsync(cancellationToken);
        return list;
    }

    public async Task<List<string>> GetTrainIds([FromQuery] DateOnly? date,
        CancellationToken cancellationToken = default)
    {
        var queryable = dbContext.TrainRuns.AsNoTracking();
        if (date is not null) 
            queryable = queryable.Where(x => x.ServiceDate == date);
        
        var ids = 
            await queryable.OrderBy(x => x.TrainId).Select(x => x.TrainId).ToListAsync(cancellationToken);
        
        return ids;
    }

    public async Task UpsertCurrentPositionAsync(CurrentTrainPosition position, CancellationToken cancellationToken)
    {
        var existing = await dbContext.CurrentTrainPosition.FindAsync([position.TrainId], cancellationToken);
        if (existing is null)
        {
            await dbContext.CurrentTrainPosition.AddAsync(position, cancellationToken);
        }
        else
        {
            existing.LocStanox       = position.LocStanox;
            existing.ReportedAt      = position.ReportedAt;
            existing.Direction       = position.Direction;
            existing.Line            = position.Line;
            existing.VariationStatus = position.VariationStatus;
            dbContext.CurrentTrainPosition.Update(existing);
        }
    }
    
    public Task<CurrentTrainPosition?> FindCurrentPositionAsync(string trainId, CancellationToken cancellationToken)
        => dbContext.CurrentTrainPosition.AsNoTracking().SingleOrDefaultAsync(currentTrainPosition => 
            currentTrainPosition.TrainId == trainId, cancellationToken);

    // NEW: bulk/filtered (any filter can be null)
    public async Task<IReadOnlyList<CurrentTrainPosition>> FindCurrentPositionsAsync(
        string? trainId,
        string? stanox,
        DateTimeOffset? since,
        int take,
        CancellationToken cancellationToken)
    {
        var trainPositions = dbContext.CurrentTrainPosition.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(trainId))
            trainPositions = trainPositions.Where(currentTrainPosition => currentTrainPosition.TrainId == trainId);

        if (!string.IsNullOrWhiteSpace(stanox))
            trainPositions = trainPositions.Where(currentTrainPosition => currentTrainPosition.LocStanox == stanox);

        if (since is { })
            trainPositions = trainPositions.Where(x => x.ReportedAt >= since.Value);

        // newest first, cap the result size
        trainPositions = trainPositions.OrderByDescending(currentTrainPosition => 
            currentTrainPosition.ReportedAt).Take(Math.Clamp(take, 1, 10_000));

        return await trainPositions.ToListAsync(cancellationToken);
    }

    public async Task<bool> DeleteAllOldTrainPositions(int dayOffset, CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow - TimeSpan.FromDays(dayOffset);

        try
        {
            var deleted = await dbContext.CurrentTrainPosition
                .Where(p => p.ReportedAt < cutoff)
                .ExecuteDeleteAsync(cancellationToken);

            if (deleted > 0)
                log.LogInformation("Deleted {count} old CurrentTrainPosition rows older than {cutoff}.", deleted, cutoff);

            return deleted > 0;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Failed deleting old CurrentTrainPosition rows.");
            return false;
        }
    }

    public async Task<bool> DeleteAllOldMovementEvents(int dateOffset, CancellationToken cancellationToken)
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

            return deleted > 0;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Failed deleting old MovementEvent rows.");
            return false;
        }
    }

    public async Task<bool> DeleteAllOldTrains(int dayOffset, CancellationToken cancellationToken)
    {
        var cutoffDate = DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(-dayOffset));

        try
        {
            // Delete dependents first (safe if you have FK constraints)
            var oldTrainIds = dbContext.TrainRuns
                .AsNoTracking()
                .Where(r => r.ServiceDate < cutoffDate)
                .Select(r => r.TrainId);

            var movementDeleted = await dbContext.MovementEvents
                .Where(m => oldTrainIds.Contains(m.TrainId))
                .ExecuteDeleteAsync(cancellationToken);

            var positionDeleted = await dbContext.CurrentTrainPosition
                .Where(p => oldTrainIds.Contains(p.TrainId))
                .ExecuteDeleteAsync(cancellationToken);

            var runsDeleted = await dbContext.TrainRuns
                .Where(r => r.ServiceDate < cutoffDate)
                .ExecuteDeleteAsync(cancellationToken);

            var total = movementDeleted + positionDeleted + runsDeleted;

            if (total > 0)
                log.LogInformation(
                    "Deleted old trains before {cutoffDate}: TrainRuns={runs}, MovementEvents={movements}, CurrentTrainPosition={positions}.",
                    cutoffDate, runsDeleted, movementDeleted, positionDeleted);

            return total > 0;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Failed deleting old TrainRuns (and dependent rows).");
            return false;
        }
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => dbContext.SaveChangesAsync(cancellationToken);
}
