using Microsoft.AspNetCore.Mvc;
using TTT.TrainData.Model;

namespace TTT.TrainData.Controller;

[ApiController]
[Route("api/trains")]
public sealed class TrainsController(TrainDataModel trainDataModel) : ControllerBase
{
    
    /// <summary>
    /// Gets train poition using ID from DB
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
            return BadRequest("Error: TrainId invalid.");
        }
        
        var result = await trainDataModel.GetPosition(trainId, cancellationToken);

        if (result == null)
        {
            return BadRequest("Info: Train not found.");
        }
        
        return Ok(result);
    }

    /// <summary>
    /// 
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
            return BadRequest("Error: TrainId invalid.");
        }
        
        var result = await trainDataModel.GetMovements(trainId, from, timeOffset, cancellationToken);

        if (result.Count == 0)
        {
            return BadRequest("Info: No trains found.");
        }
        
        return Ok(result);
    }

    /// <summary>
    /// Gets train Ids from DB
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
            return BadRequest("Error: Date cannot be null.");
        }

        var result = await trainDataModel.GetTrainIds(date, cancellationToken);

        if (result.Count == 0)
        {
            return BadRequest("Info: TrainId not found.");
        }
        
        return Ok(result);
    }
}