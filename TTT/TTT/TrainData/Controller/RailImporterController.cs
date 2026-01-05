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
}