using Microsoft.AspNetCore.Mvc;

namespace TTT.TrainData.Controller;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    /// <summary>
    /// Simple POST to verify the server is running.
    /// </summary>
    [HttpPost("PingServer")]
    [ProducesResponseType(typeof(PingResponse), StatusCodes.Status200OK)]
    public ActionResult<PingResponse> PingServer()
    {
        return Ok(new PingResponse
        {
            Status = "ok",
            ServerUtc = DateTime.UtcNow
        });
    }
}

public class PingResponse
{
    public string Status { get; set; } = "ok";
    public DateTime ServerUtc { get; set; }
}