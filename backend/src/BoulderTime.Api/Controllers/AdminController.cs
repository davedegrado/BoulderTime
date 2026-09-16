using BoulderTime.Application.Admin;
using BoulderTime.Application.Candidates;
using BoulderTime.Application.Common;
using BoulderTime.Application.Gyms;
using BoulderTime.Application.Staff;
using BoulderTime.Domain.Candidates;
using BoulderTime.Domain.Gyms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoulderTime.Api.Controllers;

/// <summary>BoulderTime platform administration. Every action re-checks the platform-admin flag in the database.</summary>
[ApiController]
[Route("api/admin")]
[Authorize]
[Produces("application/json")]
public sealed class AdminController(AdminService admin, GymCandidateService candidates) : ControllerBase
{
    [HttpGet("dashboard")]
    public Task<AdminDashboardDto> Dashboard(CancellationToken ct) => admin.GetDashboardAsync(ct);

    [HttpGet("gyms")]
    public Task<PagedResult<AdminGymDto>> Gyms([FromQuery] string? q, [FromQuery] GymStatus? status, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        admin.ListGymsAsync(q, status, page, pageSize, ct);

    [HttpPost("gyms")]
    public async Task<ActionResult<GymDetailDto>> CreateGym([FromBody] CreateGymRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await admin.CreateGymAsync(request, ct));

    [HttpPut("gyms/{gymId:guid}/status")]
    public Task<GymDetailDto> SetGymStatus(Guid gymId, [FromBody] SetGymStatusRequest request, CancellationToken ct) =>
        admin.SetGymStatusAsync(gymId, request, ct);

    [HttpPost("gyms/{gymId:guid}/owner-invitations")]
    public async Task<ActionResult<GymInvitationDto>> InviteOwner(Guid gymId, [FromBody] InviteOwnerRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await admin.InviteOwnerAsync(gymId, request, ct));

    [HttpGet("gym-candidates")]
    public Task<PagedResult<GymCandidateDto>> Candidates([FromQuery] GymCandidateStatus? status, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        candidates.AdminListAsync(status, page, pageSize, ct);

    [HttpPut("gym-candidates/{candidateId:guid}/status")]
    public Task<GymCandidateDto> SetCandidateStatus(Guid candidateId, [FromBody] UpdateCandidateStatusRequest request, CancellationToken ct) =>
        candidates.AdminSetStatusAsync(candidateId, request, ct);

    [HttpGet("users")]
    public Task<PagedResult<AdminUserDto>> Users([FromQuery] string? q, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        admin.ListUsersAsync(q, page, pageSize, ct);
}
