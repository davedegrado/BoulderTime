using BoulderTime.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;

namespace BoulderTime.Api.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController(AppDbContext db) : ControllerBase
{
    public sealed record HealthDto(string Status, bool Database);

    /// <summary>Liveness plus a database connectivity check. Public; reveals no configuration.</summary>
    [HttpGet]
    public async Task<ActionResult<HealthDto>> Get(CancellationToken ct)
    {
        var dbOk = await db.Database.CanConnectAsync(ct);
        var dto = new HealthDto(dbOk ? "ok" : "degraded", dbOk);
        return dbOk ? Ok(dto) : StatusCode(StatusCodes.Status503ServiceUnavailable, dto);
    }
}
