using Microsoft.AspNetCore.Mvc;

namespace TTT.TrainData.Controller;

[ApiController]
[Route("api/admin/movements")]
public sealed class MovementsIngestController : ControllerBase
{
    private readonly IMovementsIngestionService  _movementsIngestionService;
    private readonly ILogger<MovementsIngestController> _log;
    private static readonly SemaphoreSlim Gate = new(1, 1);

    public MovementsIngestController(IServiceScopeFactory scopeFactory, ILogger<MovementsIngestController> log)
    {
        var scope = scopeFactory.CreateScope();
        _movementsIngestionService = scope.ServiceProvider.GetRequiredService<IMovementsIngestionService>();
        
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
        // TODO add error catch for log 
        await Task.Run(() => _movementsIngestionService.IntegstOnceServiceAsync(topic, maxMessages, maxSeconds, cancellationToken), cancellationToken);
        
        return Ok();
    }
}
