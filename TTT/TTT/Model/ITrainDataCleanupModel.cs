namespace TTT.Model;

public interface ITrainDataCleanupModel
{
    Task<int> DeleteAllOldTrainPositions(int dayOffest, CancellationToken cancellationToken);
    
    Task<int> DeleteAllOldMovementEvents(int dayOffest, CancellationToken cancellationToken);
    
    Task<int> DeleteAllOldTrains(int dateOffest, CancellationToken cancellationToken);

    Task<int> DeleteAllMovementData(int dataOffest, CancellationToken cancellationToken);
}