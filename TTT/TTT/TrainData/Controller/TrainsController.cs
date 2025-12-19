using Microsoft.AspNetCore.Mvc;
using TTT.TrainData.Model;

namespace TTT.TrainData.Controller;

[ApiController]
[Route("api/trains")]
public sealed class TrainsController(TrainDataModel trainDataModel, ILogger<TrainsController> log) : ControllerBase
{
    
    /// <summary>
    /// Gets train position using ID from database
    /// </summary>
    /// <param name="trainId"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet("/position")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult?> GetPosition(string trainId, CancellationToken cancellationToken)
    {
        if (trainId.Equals(""))
        {
            log.LogError("TrainId invalid.");
            return BadRequest("Error: TrainId invalid.");
        }
        
        var result = await trainDataModel.GetPosition(trainId, cancellationToken);

        if (result == null)
        {
            log.LogInformation("Train not found.");
            return BadRequest("Info: Train not found.");
        }
        
        return Ok(result);
    }

    /// <summary>
    /// Get movement data from database on trainID
    /// </summary>
    /// <param name="trainId"></param>
    /// <param name="from"></param>
    /// <param name="timeOffset"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet("/movements")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMovements(string trainId, [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? timeOffset, CancellationToken cancellationToken)
    {
        if (trainId.Equals(""))
        {
            log.LogError("TrainId invalid.");
            return BadRequest("Error: TrainId invalid.");
        }
        
        var result = await trainDataModel.GetMovements(trainId, from, timeOffset, cancellationToken);

        if (result.Count == 0)
        {
            log.LogInformation("No trains found.");
            return BadRequest("Info: No trains found.");
        }
        
        return Ok(result);
    }

    /// <summary>
    /// Gets train Ids from database
    /// </summary>
    /// <param name="date"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet("/trainIDs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTrainIds([FromQuery] DateOnly? date, 
        CancellationToken cancellationToken = default)
    {
        if (date.Equals(null))
        {
            log.LogError("Error: Date cannot be null.");
            return BadRequest("Error: Date cannot be null.");
        }

        var result = await trainDataModel.GetTrainIds(date, cancellationToken);

        if (result.Count == 0)
        {
            log.LogInformation("TrainIds not found on {date}", date);
            return BadRequest("Info: TrainId not found.");
        }
        
        return Ok(result);
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
        
            if (await trainDataModel.DeleteAllOldMovementEvents(dayOffset, cancellationToken))
                deleteCount++;
        
            if(await trainDataModel.DeleteAllOldTrainPositions(dayOffset, cancellationToken))
                deleteCount++;
        
            if(await trainDataModel.DeleteAllOldTrains(dayOffset, cancellationToken))
                deleteCount++;
        
            log.LogInformation("Total sets deleted: {deleteCount}", deleteCount);
        
            return Ok();
        }
        
        log.LogError("dayOffset invalid. Cannot be 0 or less");
        return BadRequest("Error: day offset invalid.");
    }
}