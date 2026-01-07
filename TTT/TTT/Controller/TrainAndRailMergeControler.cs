using Microsoft.AspNetCore.Mvc;
using TTT.Model;

namespace TTT.Controller;

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
}