using BoulderTime.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoulderTime.Api.Controllers;

/// <summary>Reporting a person, and BoulderTime suspending an account across the whole platform.</summary>
[ApiController]
[Authorize]
[Produces("application/json")]
public sealed class UserModerationController(UserModerationService moderation) : ControllerBase
{
    /// <summary>Anyone signed in reports a person to BoulderTime. The person is not told who reported them.</summary>
    [HttpPost("api/users/{userId:guid}/reports")]
    public async Task<IActionResult> Report(Guid userId, [FromBody] ReportUserRequest request, CancellationToken ct)
    {
        await moderation.ReportAsync(userId, request, ct);
        return NoContent();
    }

    [HttpGet("api/admin/user-reports")]
    public Task<IReadOnlyList<UserReportDto>> List([FromQuery] bool includeHandled, CancellationToken ct) =>
        moderation.ListReportsAsync(includeHandled, ct);

    [HttpPost("api/admin/user-reports/{reportId:guid}/handle")]
    public Task<UserReportDto> Handle(Guid reportId, [FromBody] HandleUserReportRequest request, CancellationToken ct) =>
        moderation.HandleReportAsync(reportId, request, ct);

    [HttpPut("api/admin/users/{userId:guid}/suspension")]
    public async Task<IActionResult> Suspend(Guid userId, [FromBody] SuspendUserRequest request, CancellationToken ct)
    {
        await moderation.SuspendAsync(userId, request, ct);
        return NoContent();
    }

    [HttpDelete("api/admin/users/{userId:guid}/suspension")]
    public async Task<IActionResult> Reinstate(Guid userId, CancellationToken ct)
    {
        await moderation.ReinstateAsync(userId, ct);
        return NoContent();
    }
}
