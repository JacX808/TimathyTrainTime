namespace TTT.Model;

public interface ITrainAndRailMergeModel
{
    Task<int> MergeTrainAndRailDataAsync(CancellationToken cancellationToken);
}