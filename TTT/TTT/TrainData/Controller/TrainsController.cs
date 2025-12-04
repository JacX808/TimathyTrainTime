using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TTT.Database;

namespace TTT.TrainData.Controller;

[ApiController]
[Route("api/trains")]
public sealed class TrainsController(TttDbContext dbContext) : ControllerBase
{
    [HttpGet("/position")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetPosition(string trainId, CancellationToken cancellationToken)
    {
        var pos = await dbContext.CurrentPositions.AsNoTracking().SingleOrDefaultAsync(currentTrainPosition => 
            currentTrainPosition.TrainId == trainId, cancellationToken);
        
        if (pos is null) 
            return NotFound();
        
        return Ok(pos);
    }

    [HttpGet("/movements")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetMovements(string trainId, [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, CancellationToken ct)
    {
        var queryable = dbContext.MovementEvents.AsNoTracking().Where(x => x.TrainId == trainId);
        if (from is { }) queryable = queryable.Where(x => x.ActualTimestampMs >= from.Value.ToUnixTimeMilliseconds());
        if (to   is { }) queryable = queryable.Where(x => x.ActualTimestampMs <= to.Value.ToUnixTimeMilliseconds());
        var list = await queryable.OrderBy(x => x.ActualTimestampMs).ToListAsync(ct);
        return Ok(list);
    }

    [HttpGet("/trainIDs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetTrainIds([FromQuery] DateOnly? date, CancellationToken ct)
    {
        var hold = date.ToString();
        var queryable = dbContext.TrainRuns.AsNoTracking();
        if (date is { }) queryable = queryable.Where(x => x.ServiceDate == date);
        var ids = await queryable.OrderBy(x => x.TrainId).Select(x => x.TrainId).ToListAsync(ct);
        return Ok(ids);
    }
}