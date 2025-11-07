using Microsoft.AspNetCore.Mvc;

namespace TTT.TrainData.IDs;

public class TrainIdController : ControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<string>>> GetAllTrainIds(CancellationToken ct)
    {
        /*var ids = await _db.CurrentPositions
            .AsNoTracking()
            .Select(p => p.TrainId)
            .OrderBy(id => id)
            .ToListAsync(ct);*/

        return Ok("");
    }
}