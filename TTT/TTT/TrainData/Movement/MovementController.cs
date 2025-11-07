using Microsoft.AspNetCore.Mvc;
using TTT.Database;

namespace TTT.TrainData.Movement;

[ApiController]
[Route("api/trains")]
public sealed class MovementController : ControllerBase
{

    private readonly TttDbContext _db;
    public MovementController(TttDbContext db) => _db = db;

    [HttpGet("{trainId}/position")]
    public async Task<IActionResult> GetPosition(string trainId, CancellationToken ct)
    {
        var pos = await _db.CurrentPositions.FindAsync(new object[]{ trainId }, ct);
        if (pos is null) return NotFound();

        var loc = await _db.Locations.FindAsync(new object[]{ pos.LocStanox }, ct);
        return Ok(new {
            trainId,
            reportedAt = pos.ReportedAt,
            stanox = pos.LocStanox,
            location = loc?.Name,
            crs = loc?.Crs,
            lat = loc?.Lat, lon = loc?.Lon,
            direction = pos.Direction, line = pos.Line
        });
    }
        
}