using Microsoft.AspNetCore.Mvc;
using TTT.TrainData.Model;

namespace TTT.TrainData.Controller;

/// <summary>
/// Controller for the rail data endpoint.
/// This will import all loc positions to the database for the map position usage
/// </summary>
/// <param name="railReferenceImportModel"></param>
/// <param name="log"></param>
public class RailImporterController(IRailReferenceImportModel railReferenceImportModel, ILogger<RailImporterController> log)
    : ControllerBase
{
 
    /// <summary>
    /// Import/Update all stanox and PlanB data
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet("/railImporter")]
    public async Task<IActionResult> ImportRailDataAsync(CancellationToken cancellationToken)
    {
        try
        {
            var result = await railReferenceImportModel.ImportRailAsync(cancellationToken);
            log.LogInformation($"{result} rail data import.");
            return Ok($"{result} Rail data import.");
        }
        catch (Exception ex)
        {
            log.LogError(ex.Message);
            return StatusCode(500, "Internal Server Error");
        }
    }

    /// <summary>
    /// Check and update corpus data
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpPost("/corpusCheck")]
    public async Task<IActionResult> RunCorpusCheckAsync(CancellationToken cancellationToken)
    {
        var result = await railReferenceImportModel.RunCorpusCheckAsync(cancellationToken);

        if (result)
        {
            log.LogInformation("Corpus check successful.");
            return Ok("Corpus check successful.");
        }
        
        log.LogError("Corpus check failed.");
        return StatusCode(500, "Corpus check failed.");
    }
    
    /// <summary>
    /// Return all rail stanox with lat and long.
    /// </summary>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet("/allraillocationsLite")]
    public async Task<IActionResult> GetAllRailLocationsLiteAsync(CancellationToken cancellationToken)
    {
        var result = await railReferenceImportModel.GetAllRailLocationLiteAsync(cancellationToken);

        if (result.Count < 1)
        {
            log.LogInformation("No Rail locations found.");
            return StatusCode(200, "No Rail locations found.");
        }
        
        return Ok(result);
    }

    /// <summary>
    /// Return all data on stanox
    /// </summary>
    /// <param name="stanox"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet("/getrailLocationByStanox")]
    public async Task<IActionResult> GetRailLocationByStanoxAsync([FromQuery] string stanox, CancellationToken cancellationToken)
    {
        if(stanox.Equals(""))
            return BadRequest("Stanox must be specified.");
        
        var result = await railReferenceImportModel.GetRailLocationAsync(stanox, cancellationToken);

        if (result == null)
        {
            log.LogInformation("Rail location not found.");
            return NotFound("Rail location not found.");
        }
        
        return Ok(result);
    }
}