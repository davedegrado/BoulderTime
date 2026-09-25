using BoulderTime.Application.Leaderboards;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoulderTime.Api.Controllers;

/// <summary>
/// Who shows up in leaderboards: the climber's own choice, the gym's reports, and BoulderTime's decisions.
/// </summary>
[ApiController]
[Authorize]
[Produces("application/json")]
public sealed class LeaderboardVisibilityController(LeaderboardVisibilityService service) : ControllerBase
{
    /// <summary>The climber hides or shows themselves in every leaderboard.</summary>
    [HttpPut("api/users/me/leaderboard-visibility")]
    public async Task<IActionResult> SetOwn([FromBody] LeaderboardVisibilityRequest request, CancellationToken ct)
    {
        await service.SetOwnVisibilityAsync(request, ct);
        return NoContent();
    }

    /// <summary>Gym staff report a climber whose results look implausible. The gym cannot exclude anyone itself.</summary>
    [HttpPost("api/gyms/{gymId:guid}/leaderboard-reports")]
    public Task<LeaderboardReportDto> Report(Guid gymId, [FromBody] ReportClimberRequest request, CancellationToken ct) =>
        service.ReportAsync(gymId, request, ct);

    [HttpGet("api/admin/leaderboard-reports")]
    public Task<IReadOnlyList<LeaderboardReportDto>> List([FromQuery] bool includeHandled, CancellationToken ct) =>
        service.ListReportsAsync(includeHandled, ct);

    [HttpPost("api/admin/leaderboard-reports/{reportId:guid}/handle")]
    public Task<LeaderboardReportDto> Handle(Guid reportId, [FromBody] HandleLeaderboardReportRequest request, CancellationToken ct) =>
        service.HandleReportAsync(reportId, request, ct);

    [HttpGet("api/admin/leaderboard-exclusions")]
    public Task<IReadOnlyList<ExcludedClimberDto>> Excluded(CancellationToken ct) => service.ListExcludedAsync(ct);

    [HttpDelete("api/admin/leaderboard-exclusions/{userId:guid}")]
    public async Task<IActionResult> Allow(Guid userId, CancellationToken ct)
    {
        await service.AllowAsync(userId, ct);
        return NoContent();
    }
}
