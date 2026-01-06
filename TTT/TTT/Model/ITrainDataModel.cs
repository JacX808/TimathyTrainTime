using Microsoft.AspNetCore.Mvc;
using TTT.DataSets;

namespace TTT.Model;

public interface ITrainDataModel
{
    Task<TrainRun?> FindTrainRunAsync(string trainId, CancellationToken cancellationToken);
    Task AddTrainRunAsync(TrainRun run, CancellationToken ct);

    /// <summary>Adds a movement event. Returns true if inserted; false if it was a duplicate (unique index hit).</summary>
    Task<bool> AddMovementEventAsync(MovementEvent movementEvent, CancellationToken cancellationToken, bool ignoreDuplicates = true);

    /// <summary>Creates or updates the current position snapshot for a train.</summary>
    Task UpsertCurrentPositionAsync(CurrentTrainPosition position, CancellationToken cancellationToken);
    
    Task<CurrentTrainPosition?> FindCurrentPositionAsync(string trainId, CancellationToken cancellationToken);

    Task<List<MovementEvent>> GetMovements(string trainId, DateTimeOffset? from, DateTimeOffset? timeOffset,
        CancellationToken cancellationToken);

    Task<List<string>> GetTrainIds([FromQuery] DateOnly? date,
        CancellationToken cancellationToken = default);
    
    Task<CurrentTrainPosition?> GetPosition(string trainId, CancellationToken cancellationToken);
    
    Task<IReadOnlyList<CurrentTrainPosition>> FindCurrentPositionsAsync(
        string? trainId,
        string? stanox,
        DateTimeOffset? since,
        int take,
        CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}