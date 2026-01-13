using Microsoft.EntityFrameworkCore;
using TTT.Database;
using TTT.DataSets.Train;

namespace TTT.Model;

public class MinimumTrainDataModel(TttDbContext dbContext, ILogger<TrainAndRailMergeModel> log) : IMinimumTrainDataModel
{
    
    public Task<TrainMinimumData?> FindDataByIdAsync(string trainId, CancellationToken cancellationToken) 
        => dbContext.TrainMinimumData.AsNoTracking().SingleOrDefaultAsync(trainRun => 
            trainRun.TrainId == trainId, cancellationToken);

    public async Task<string?> FindStanoxByIdAsync(string trainId, CancellationToken cancellationToken)
    {
        var trainData = await dbContext.TrainMinimumData.SingleOrDefaultAsync(trainMinimumData =>
            trainMinimumData.TrainId == trainId, cancellationToken: cancellationToken);

        if (trainData != null)
            return trainData.LocStanox;
        
        log.LogError("Train data not found");
        return null;

    }
    
    public async Task<bool> AddMinimumTrainDataAsync(TrainMinimumData trainMinimumData,
        CancellationToken cancellationToken)
    {
        try
        {
            var existing = await dbContext.TrainMinimumData.FindAsync([trainMinimumData.TrainId],
                cancellationToken);

            if (existing is null)
            {
                await dbContext.TrainMinimumData.AddAsync(trainMinimumData, cancellationToken);
            }
            else
            {
                existing.TrainId = trainMinimumData.TrainId;
                existing.LocStanox = trainMinimumData.LocStanox;
                existing.NextLocStanox = trainMinimumData.NextLocStanox;
                existing.LastSeenUtc = trainMinimumData.LastSeenUtc;
                existing.VariationStatus = trainMinimumData.VariationStatus;
                dbContext.TrainMinimumData.Update(existing);
            }
            
            return true;
        }
        catch (DbUpdateException dbUpdateException)
        {
            dbContext.Entry(trainMinimumData).State = EntityState.Detached;

            log.LogInformation(dbUpdateException, "Duplicate TrainMinimumData ignored for {train_id}@{last}/{Loc}",
                trainMinimumData.TrainId, trainMinimumData.LastSeenUtc, trainMinimumData.LocStanox);
            return false;
        }
    }

    public async Task<int> DeleteOldTrainDataAsync(int dateOffset, CancellationToken cancellationToken)
    {
        var cutoff = DateTimeOffset.UtcNow - TimeSpan.FromDays(dateOffset);

        try
        {
            var deleted = await dbContext.TrainMinimumData
                .Where(trainMinimumData => trainMinimumData.LastSeenUtc < cutoff)
                .ExecuteDeleteAsync(cancellationToken);

            if (deleted > 0)
                log.LogInformation("Deleted {count} old TrainMinimumData rows older than {cutoff}.",
                    deleted, cutoff);

            return deleted;
        }
        catch (Exception ex)
        {
            log.LogError(ex, "Failed deleting old MovementEvent rows.");
            return 0;
        }
    }
    
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken) 
        => dbContext.SaveChangesAsync(cancellationToken);
}