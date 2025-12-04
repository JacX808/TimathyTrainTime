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

    public Task<TrainRun?> FindTrainRunAsync(string trainId, CancellationToken ct)
        => _dbContext.TrainRuns.AsNoTracking().SingleOrDefaultAsync(x => x.TrainId == trainId, ct);

    public async Task AddTrainRunAsync(TrainRun run, CancellationToken ct)
        => await _dbContext.TrainRuns.AddAsync(run, ct);

    public async Task<bool> AddMovementEventAsync(MovementEvent evt, CancellationToken ct, bool ignoreDuplicates = true)
    {
        await _dbContext.MovementEvents.AddAsync(evt, ct);
        if (!ignoreDuplicates) return true;

        try
        {
            await _dbContext.SaveChangesAsync(ct); // persist early to surface dup quickly
            return true;
        }
        catch (DbUpdateException ex)
        {
            // Likely unique index violation on (TrainId, ActualTimestampMs, LocStanox, EventType)
            _dbContext.Entry(evt).State = EntityState.Detached;
            _log.LogDebug(ex, "Duplicate MovementEvent ignored for {TrainId}@{Ts}/{Loc}/{Type}",
                evt.TrainId, evt.ActualTimestampMs, evt.LocStanox, evt.EventType);
            return false;
        }
    }

    public async Task UpsertCurrentPositionAsync(CurrentTrainPosition position, CancellationToken ct)
    {
        var existing = await _dbContext.CurrentPositions.FindAsync([position.TrainId], ct);
        if (existing is null)
        {
            await _dbContext.CurrentPositions.AddAsync(position, ct);
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
    
    public Task<CurrentTrainPosition?> FindCurrentPositionAsync(string trainId, CancellationToken ct)
        => _dbContext.CurrentPositions.AsNoTracking().SingleOrDefaultAsync(x => x.TrainId == trainId, ct);

    // NEW: bulk/filtered (any filter can be null)
    public async Task<IReadOnlyList<CurrentTrainPosition>> FindCurrentPositionsAsync(
        string? trainId,
        string? stanox,
        DateTimeOffset? since,
        int take,
        CancellationToken ct)
    {
        var q = _dbContext.CurrentPositions.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(trainId))
            q = q.Where(x => x.TrainId == trainId);

        if (!string.IsNullOrWhiteSpace(stanox))
            q = q.Where(x => x.LocStanox == stanox);

        if (since is { })
            q = q.Where(x => x.ReportedAt >= since.Value);

        // newest first, cap the result size
        q = q.OrderByDescending(x => x.ReportedAt).Take(Math.Clamp(take, 1, 10_000));

        return await q.ToListAsync(ct);
    }

    public Task<int> SaveChangesAsync(CancellationToken ct) => _dbContext.SaveChangesAsync(ct);
}
