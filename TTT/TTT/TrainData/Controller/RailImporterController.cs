using Microsoft.AspNetCore.Mvc;
using TTT.TrainData.Model;

namespace TTT.TrainData.Controller;

/// <summary>
/// Controller for the rail data endpoint.
/// This will import all loc positions to the database for the map position usage
/// </summary>
/// <param name="railReferenceImportModel"></param>
/// <param name="log"></param>
public class RailImporterController(RailReferenceImportModel railReferenceImportModel, ILogger<RailImporterController> log)
    : ControllerBase
{
    
    [HttpGet("/rail importer")]
    public async Task<IActionResult> ImportRailDataAsync(CancellationToken cancellationToken)
    {
        bool result = await railReferenceImportModel.ImportRailAsync(cancellationToken);

        if (result)
        {
            log.LogInformation("Rail data imported.");
            return Ok("Rail data imported.");
        }

        log.LogError("Rail data import failed.");
        return Ok("Error: Rail data import failed.");
        
    }
}