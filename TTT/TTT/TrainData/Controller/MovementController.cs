using Microsoft.AspNetCore.Mvc;

namespace TTT.TrainData.Controller;

[ApiController]
[Route("api/trains")]
public sealed class MovementController : ControllerBase
{

    [HttpGet("{trainId}/position")]
    public Task<IActionResult> GetPosition(string trainId, CancellationToken ct)
    {
        return Task.FromResult<IActionResult>(null!);
    }
        
}