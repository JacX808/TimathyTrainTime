using TTT.Model;

namespace TTT.Service;

public sealed class LoopingIngestService : BackgroundService
{
    private readonly ILogger<LoopingIngestService> _logger;
    private readonly IMovementsIngestionModel _movementsIngestionModel;
    private readonly IMinimumTrainDataModel _minimumTrainDataModel;
    private int _timeOutCounter = 0;

    public LoopingIngestService(IServiceScopeFactory serviceScopeFactory, ILogger<LoopingIngestService> logger)
    {
        _movementsIngestionModel = serviceScopeFactory.CreateScope().ServiceProvider
            .GetRequiredService<IMovementsIngestionModel>();
        
        _minimumTrainDataModel = serviceScopeFactory.CreateScope().ServiceProvider
            .GetRequiredService<IMinimumTrainDataModel>();
        
        _logger = logger;
        logger.LogInformation("Ingest Service startup");
    }

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (true)
            {
                await _minimumTrainDataModel.DeleteOldTrainDataAsync(cancellationToken);
                await _movementsIngestionModel.IntegstOnceServiceAsync(Utility.Constants.IngestTopic, 
                    Utility.Constants.maxIngestMessage, Utility.Constants.maxIngestSeconds,
                    cancellationToken);
               
                await Task.Delay(Utility.Constants.maxIngestSeconds*100, cancellationToken).WaitAsync(cancellationToken);
            }
            
        }
        catch (Exception e)
        {
            _logger.LogError("Ingest Service shutdown, retrying in 5 seconds...");
            _timeOutCounter++;
            await Task.Delay(5000, cancellationToken).WaitAsync(cancellationToken);
            
            if(_timeOutCounter < 5)
                await ExecuteAsync(cancellationToken);
        }

        _logger.LogInformation("Ingest Service shutdown");
    }
}