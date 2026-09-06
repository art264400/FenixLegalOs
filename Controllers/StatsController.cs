using FenixLegalOs.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace FenixLegalOs.Controllers;

[ApiController]
[Route("api/[controller]")]
public class StatsController : ControllerBase
{
    private readonly SessionRepository _sessions;

    public StatsController(SessionRepository sessions)
    {
        _sessions = sessions;
    }

    [HttpGet("benchmark")]
    public IActionResult GetBenchmarkStats()
    {
        var stats = _sessions.GetBenchmarkStats();
        return Ok(stats);
    }
}
