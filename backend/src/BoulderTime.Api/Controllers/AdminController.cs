using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Admin;
using BoulderTime.Application.Candidates;
using BoulderTime.Application.Common;
using BoulderTime.Application.Gyms;
using BoulderTime.Application.Staff;
using BoulderTime.Domain.Boulders;
using BoulderTime.Domain.Candidates;
using BoulderTime.Domain.Gyms;
using BoulderTime.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BoulderTime.Api.Controllers;

/// <summary>BoulderTime platform administration. Every action re-checks the platform-admin flag in the database.</summary>
[ApiController]
[Route("api/admin")]
[Authorize]
[Produces("application/json")]
public sealed class AdminController(AdminService admin, GymCandidateService candidates, BetaTestService betaTest,
    PhotoRecompressionService recompression) : ControllerBase
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

    /// <summary>
    /// Launch day: every boulder of the gym off the wall becomes a trial boulder (kept in history, no points, in no
    /// leaderboard). Running it again adds the ones taken down since. Not for use after launch: it would also mark the
    /// gym's ordinary retraces.
    /// </summary>
    [HttpPost("gyms/{gymId:guid}/beta-test"), EnableRateLimiting(RateLimiting.WritePolicy)]
    public Task<BetaTestResultDto> MarkBetaTest(Guid gymId, [FromBody] BetaTestRequest request, CancellationToken ct) =>
        betaTest.MarkAsync(gymId, request, ct);

    /// <summary>Takes the trial mark off every boulder of the gym.</summary>
    [HttpPost("gyms/{gymId:guid}/beta-test/undo"), EnableRateLimiting(RateLimiting.WritePolicy)]
    public Task<BetaTestResultDto> UnmarkBetaTest(Guid gymId, [FromBody] BetaTestRequest request, CancellationToken ct) =>
        betaTest.UnmarkAsync(gymId, request, ct);

    /// <summary>
    /// Lighter boulder pictures for a gym: what is left to do and what was done. The device encodes each picture again,
    /// uploads the copy and swaps it in; the old files stay until confirmed, so it can all be undone.
    /// </summary>
    [HttpGet("gyms/{gymId:guid}/lighter-pictures")]
    public Task<RecompressionOverviewDto> LighterPictures(Guid gymId, CancellationToken ct) => recompression.OverviewAsync(gymId, ct);

    [HttpGet("gyms/{gymId:guid}/lighter-pictures/{boulderId:guid}/{file}")]
    public async Task<IActionResult> LighterPictureSource(Guid gymId, Guid boulderId, PhotoFile file, CancellationToken ct)
    {
        var (content, type) = await recompression.FileAsync(gymId, boulderId, file, ct);
        return File(content, type);
    }

    [HttpPost("gyms/{gymId:guid}/lighter-pictures/uploads")]
    public Task<UploadTicket> LighterPictureUpload(Guid gymId, [FromBody] RecompressionUploadRequest request, CancellationToken ct) =>
        recompression.CreateUploadAsync(gymId, request, ct);

    [HttpPost("gyms/{gymId:guid}/lighter-pictures")]
    public Task<AppliedRecompressionDto> ApplyLighterPicture(Guid gymId, [FromBody] ApplyRecompressionRequest request, CancellationToken ct) =>
        recompression.ApplyAsync(gymId, request, ct);

    /// <summary>Every picture of the gym not yet confirmed goes back to its old file.</summary>
    [HttpPost("gyms/{gymId:guid}/lighter-pictures/undo"), EnableRateLimiting(RateLimiting.WritePolicy)]
    public Task<RecompressionResultDto> UndoLighterPictures(Guid gymId, [FromBody] BetaTestRequest request, CancellationToken ct) =>
        recompression.RollbackAsync(gymId, request, ct);

    /// <summary>Deletes the old files of the gym's replaced pictures. Final.</summary>
    [HttpPost("gyms/{gymId:guid}/lighter-pictures/confirm"), EnableRateLimiting(RateLimiting.WritePolicy)]
    public Task<RecompressionResultDto> ConfirmLighterPictures(Guid gymId, [FromBody] BetaTestRequest request, CancellationToken ct) =>
        recompression.FinalizeAsync(gymId, request, ct);

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
