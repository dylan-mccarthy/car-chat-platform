using CarChat.Core.Services;
using Microsoft.AspNetCore.Mvc;

namespace CarChat.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController(ISessionManager sessionManager) : ControllerBase
{
    [HttpGet]
    public IActionResult GetHealth() =>
        Ok(new { status = "ok", timestamp = DateTime.UtcNow });

    [HttpGet("sessions")]
    public IActionResult GetSessions()
    {
        var stats = sessionManager.GetStats();
        return Ok(new
        {
            appConnections = stats.AppConnections,
            agentConnections = stats.AgentConnections,
            pairedSessions = stats.PairedSessions,
        });
    }
}
