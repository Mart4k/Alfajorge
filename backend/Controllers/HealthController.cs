using AlfajorgeBackend.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AlfajorgeBackend.Controllers;

[ApiController]
[Route("api")]
public class HealthController : ControllerBase
{
    private readonly AppDbContext _db;

    public HealthController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet("health")]
    public async Task<IActionResult> GetHealth()
    {
        var connected = await _db.Database.CanConnectAsync();
        return connected
            ? Ok(new { status = "ok", database = "SQLite" })
            : StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "unavailable", database = "SQLite" });
    }
}
