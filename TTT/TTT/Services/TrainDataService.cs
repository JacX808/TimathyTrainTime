// File: Services/TrainDataService.cs
using Microsoft.EntityFrameworkCore;
using TTT.Database;
using TTT.DataSets;

namespace TTT.Services;

public sealed class TrainDataService : ITrainDataService
{
    private readonly TttDbContext _dbContext;
    private readonly ILogger<TrainDataService> _log;

    public TrainDataService(TttDbContext dbContext, ILogger<TrainDataService> log)
    {
        _dbContext = dbContext;
        _log = log;
    }

    public Task<TrainRun?> FindTrainRunAsync(string trainId, CancellationToken cancellationToken)
        => _dbContext.TrainRuns.AsNoTracking().SingleOrDefaultAsync(trainRun => trainRun.TrainId == trainId, cancellationToken);

    public async Task AddTrainRunAsync(TrainRun run, CancellationToken ct)
        => await _dbContext.TrainRuns.AddAsync(run, ct);

    public async Task<bool> AddMovementEventAsync(MovementEvent movementEvent, CancellationToken cancellationToken, bool ignoreDuplicates = true)
    {
        if (!ignoreDuplicates) 
            return true;
        
        try
        {
            await _dbContext.MovementEvents.AddAsync(movementEvent, cancellationToken);
            await _dbContext.SaveChangesAsync(cancellationToken); // persist early to surface dup quickly
            return true;
        }
        catch (DbUpdateException dbUpdateException)
        {
            // Likely unique index violation on (train_id, ActualTimestampMs, loc_stanox, event_type)
            _dbContext.Entry(movementEvent).State = EntityState.Detached;
            _log.LogDebug(dbUpdateException, "Duplicate MovementEvent ignored for {train_id}@{Ts}/{Loc}/{Type}",
                movementEvent.TrainId, movementEvent.ActualTimestampMs, movementEvent.LocStanox, movementEvent.EventType);
            return false;
        }
    }

    public async Task UpsertCurrentPositionAsync(CurrentTrainPosition position, CancellationToken cancellationToken)
    {
        var existing = await _dbContext.CurrentPositions.FindAsync([position.TrainId], cancellationToken);
        if (existing is null)
        {
            await _dbContext.CurrentPositions.AddAsync(position, cancellationToken);
        }
        else
        {
            existing.LocStanox       = position.LocStanox;
            existing.ReportedAt      = position.ReportedAt;
            existing.Direction       = position.Direction;
            existing.Line            = position.Line;
            existing.VariationStatus = position.VariationStatus;
            _dbContext.CurrentPositions.Update(existing);
        }
    }
    
    public Task<CurrentTrainPosition?> FindCurrentPositionAsync(string trainId, CancellationToken cancellationToken)
        => _dbContext.CurrentPositions.AsNoTracking().SingleOrDefaultAsync(currentTrainPosition => 
            currentTrainPosition.TrainId == trainId, cancellationToken);

    // NEW: bulk/filtered (any filter can be null)
    public async Task<IReadOnlyList<CurrentTrainPosition>> FindCurrentPositionsAsync(
        string? trainId,
        string? stanox,
        DateTimeOffset? since,
        int take,
        CancellationToken cancellationToken)
    {
        var trainPositions = _dbContext.CurrentPositions.AsNoTracking().AsQueryable();

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

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) => _dbContext.SaveChangesAsync(cancellationToken);
}
