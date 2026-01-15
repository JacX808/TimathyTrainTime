using TTT.DataSets.Train;

namespace TTT.Model;

public interface IMinimumTrainDataModel
{
    /// <summary>
    /// Find data by trainId
    /// </summary>
    /// <param name="trainId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<TrainMinimumData?> FindDataByIdAsync(string trainId, CancellationToken cancellationToken);
    
    Task<string?> FindStanoxByIdAsync(string trainId, CancellationToken cancellationToken);

    /// <summary>
    /// Add the minimumdata to the database
    /// </summary>
    /// <param name="trainMinimumData"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<bool> AddMinimumTrainDataAsync(TrainMinimumData trainMinimumData,
        CancellationToken cancellationToken);
    
    /// <summary>
    /// Delete records older than 1 day
    /// </summary>
    /// <param name="dateOffset"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task<int> DeleteOldTrainDataAsync(CancellationToken cancellationToken);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}