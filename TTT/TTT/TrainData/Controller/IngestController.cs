using Microsoft.AspNetCore.Mvc;
using TTT.TrainData.Model;

namespace TTT.TrainData.Controller;

[ApiController]
[Route("api/admin/movements")]
public sealed class IngestController : ControllerBase
{
    private readonly IMovementsIngestionService  _movementsIngestionService;
    private readonly ITrainDataModel  _trainDataModel;
    private readonly ITrainDataCleanupModel _trainDataCleanupModel;
    private readonly ILogger<IngestController> _log;
    private const int datecutoff = 1;

    public IngestController(IServiceScopeFactory scopeFactory, ITrainDataModel trainDataModel,
        ITrainDataCleanupModel trainDataCleanupModel, ILogger<IngestController> log)
    {
        var scope = scopeFactory.CreateScope();
        _movementsIngestionService = scope.ServiceProvider.GetRequiredService<IMovementsIngestionService>();
        _trainDataCleanupModel = trainDataCleanupModel;
        _trainDataModel = trainDataModel;
        _log = log;
    }

    /// <summary>
    /// Pulls TRUST movement data once, bounded by message count and/or time window,
    /// and upserts TrainRuns / MovementEvents / CurrentTrainPosition.
    /// </summary>
    [HttpPost("ingest")]
    public async Task<IActionResult> IngestOnce(
        [FromQuery] int maxMessages = 1000,
        [FromQuery] int maxSeconds = 20,
        [FromQuery] string topic = "TRAIN_MVT_ALL_TOC",
        CancellationToken cancellationToken = default)
    {

        if (topic.Equals(""))
        {
            _log.LogError("topic invalid. Cannot be empty");
            return BadRequest("topic invalid. Cannot be empty");
        }

        if (maxSeconds < 1 || maxMessages < 1)
        {
            _log.LogError("maxMessages or maxSeconds invalid. Cannot be less than 1");
            return BadRequest("maxMessages or maxSeconds invalid. Cannot be less than 1");
        }

        int totalDeleted = 
            await Task.Run(() => _trainDataCleanupModel.DeleteAllMovementData(datecutoff, cancellationToken),
                cancellationToken);
        
        _log.LogInformation("Total old records deleted: {totalDeleted}", totalDeleted);
        
        int totalAdded = await Task.Run(() => _movementsIngestionService.IntegstOnceServiceAsync(topic, maxMessages, maxSeconds,
            cancellationToken), cancellationToken);
        
        _log.LogInformation("Total new records added: {totalAdded}", totalAdded);
        
        return Ok($"{totalDeleted}  records deleted, {totalAdded} records added");
    }
    
    /// <summary>
    /// Delete all data older than today
    /// </summary>
    /// <param name="dayOffset">Date offset of how long ago you want to delete. Cannot be 0 or less</param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost("/deleteOldData")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> DeleteAllOldData([FromQuery] int dayOffset, CancellationToken cancellationToken = default)
    {
        if (dayOffset > 0)
        {
            int deleteCount = 0;

            deleteCount += await _trainDataCleanupModel.DeleteAllOldMovementEvents(dayOffset, cancellationToken);
            deleteCount += await _trainDataCleanupModel.DeleteAllOldTrainPositions(dayOffset, cancellationToken);
            deleteCount += await _trainDataCleanupModel.DeleteAllOldTrains(dayOffset, cancellationToken);
        
            _log.LogInformation("Total sets deleted: {deleteCount}", deleteCount);
        
            return Ok($"Total sets deleted: {deleteCount}");
        }
        
        _log.LogError("dayOffset invalid. Cannot be 0 or less");
        return BadRequest("Error: day offset invalid.");
    }
}
