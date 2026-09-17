using BoulderTime.Application.Leaderboards;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoulderTime.Api.Controllers;

[ApiController]
[Produces("application/json")]
public sealed class LeaderboardsController(LeaderboardService leaderboards) : ControllerBase
{
    /// <summary>Per-gym leaderboard. Public for visible gyms; signed-in viewers also get their own position.</summary>
    [HttpGet("api/gyms/{gymId:guid}/leaderboard"), AllowAnonymous]
    public Task<LeaderboardDto> Get(Guid gymId, [FromQuery] LeaderboardMetric? metric, [FromQuery] LeaderboardPeriod? period, [FromQuery] int? limit, CancellationToken ct) =>
        leaderboards.GetAsync(gymId, metric, period, limit, ct);
}
