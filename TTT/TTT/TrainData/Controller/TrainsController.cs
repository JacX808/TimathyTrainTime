using Microsoft.AspNetCore.Mvc;
using TTT.TrainData.Model;

namespace TTT.TrainData.Controller;

[ApiController]
[Route("api/trains")]
public sealed class TrainsController(ITrainDataModel trainDataModel, ILogger<TrainsController> log) : ControllerBase
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

        if (result != null) 
            return Ok(result);
        
        log.LogInformation("Train not found.");
        return BadRequest("Info: Train not found.");

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

        if (result.Count != 0) 
            return Ok(result);
        
        log.LogInformation("No trains found.");
        return BadRequest("Info: No trains found.");

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

        if (result.Count != 0) 
            return Ok(result);
        
        log.LogInformation("TrainIds not found on {date}", date);
        return Ok($"TrainIds not found on {date}");

    }
}