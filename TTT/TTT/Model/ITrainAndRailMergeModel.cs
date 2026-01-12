using TTT.DataSets.TrainAndRail;

namespace TTT.Model;

public interface ITrainAndRailMergeModel
{
    Task<int> MergeTrainAndRailDataAsync(CancellationToken cancellationToken);
    Task<List<TrainAndRailMergeLite>> GetAllTrainMapDataAsync(DateTimeOffset? dateTimeOffset,
        CancellationToken cancellationToken);
}