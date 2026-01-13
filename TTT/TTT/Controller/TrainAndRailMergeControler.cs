using Microsoft.AspNetCore.Mvc;
using TTT.Model;

namespace TTT.Controller;

[ApiController]
[Route("api/[controller]")]
public class TrainAndRailMergeControler(ITrainAndRailMergeModel trainAndRailMergeModel, 
    ILogger<TrainAndRailMergeControler> log) : ControllerBase
{
    [HttpPost("/mergeTrainAndRailData")]
    public async Task<IActionResult> MergeTrainAndRailDataAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await trainAndRailMergeModel.MergeTrainAndRailDataAsync(cancellationToken);

            if (result < 1)
            {
                log.LogInformation("No train data merged.");
                return Ok("No train data merged.");
            }
            
            return Ok($"{result} train data merged.");
        }
        catch (Exception e)
        {
            log.LogError("Error merging train data.");
            return StatusCode(500, "Error merging train data.");
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="date"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet("/getAllTrainMapData")]
    public async Task<IActionResult> GetAllTrainLocationsOnMap([FromQuery] DateTimeOffset? date, CancellationToken cancellationToken)
    {

        if (date is null)
        {
           log.LogError("Date cannot be null.");
           return BadRequest("Date cannot be null.");
        }
        
        try
        {
            // update train lite data
            var response = await trainAndRailMergeModel.MergeTrainAndRailDataAsync(cancellationToken);

            log.LogInformation($"Lite data updated: {response} merged.");
            
            // Get all train data from the lite tables 
            var result = await trainAndRailMergeModel.GetAllTrainMapDataAsync(date,
                cancellationToken);
            
            log.LogInformation($"Lite data sent to client: {result.Count} recoreds.");
            
            return Ok(result);
        }
        catch (Exception e)
        {
            log.LogError("Error getting all train map data");
            return StatusCode(500, "Error getting all train map data.");
        }
    }
}