using TTT.Database;   // TttDbContext namespace
using TTT.DataSets;   // TrainRun, MovementEvent, CurrentTrainPosition

namespace TTT.Services;

public interface ITrainDataService
{
    Task<TrainRun?> FindTrainRunAsync(string trainId, CancellationToken ct);
    Task AddTrainRunAsync(TrainRun run, CancellationToken ct);

    /// <summary>Adds a movement event. Returns true if inserted; false if it was a duplicate (unique index hit).</summary>
    Task<bool> AddMovementEventAsync(MovementEvent evt, CancellationToken ct, bool ignoreDuplicates = true);

    /// <summary>Creates or updates the current position snapshot for a train.</summary>
    Task UpsertCurrentPositionAsync(CurrentTrainPosition position, CancellationToken ct);
    
    Task<CurrentTrainPosition?> FindCurrentPositionAsync(string trainId, CancellationToken ct);
    
    Task<IReadOnlyList<CurrentTrainPosition>> FindCurrentPositionsAsync(
        string? trainId,
        string? stanox,
        DateTimeOffset? since,
        int take,
        CancellationToken ct);

    Task<int> SaveChangesAsync(CancellationToken ct);
}