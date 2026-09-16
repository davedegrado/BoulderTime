using BoulderTime.Application.Candidates;
using BoulderTime.Application.Staff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoulderTime.Api.Controllers;

/// <summary>Signed-in user's gym relationships: staff memberships, invitations, gym suggestions.</summary>
[ApiController]
[Authorize]
[Produces("application/json")]
public sealed class MeController(InvitationService invitations, GymCandidateService candidates) : ControllerBase
{
    [HttpGet("api/users/me/gyms")]
    public Task<IReadOnlyList<MyStaffGymDto>> MyStaffGyms(CancellationToken ct) => invitations.ListMyStaffGymsAsync(ct);

    [HttpGet("api/users/me/invitations")]
    public Task<IReadOnlyList<MyInvitationDto>> MyInvitations(CancellationToken ct) => invitations.ListMineAsync(ct);

    [HttpPost("api/invitations/{invitationId:guid}/accept")]
    public Task<MyStaffGymDto> Accept(Guid invitationId, CancellationToken ct) => invitations.AcceptAsync(invitationId, ct);

    [HttpPost("api/invitations/{invitationId:guid}/decline")]
    public async Task<IActionResult> Decline(Guid invitationId, CancellationToken ct)
    {
        await invitations.DeclineAsync(invitationId, ct);
        return NoContent();
    }

    [HttpGet("api/users/me/gym-candidates")]
    public Task<IReadOnlyList<GymCandidateDto>> MyCandidates(CancellationToken ct) => candidates.ListMineAsync(ct);

    [HttpPost("api/gym-candidates")]
    public async Task<ActionResult<GymCandidateDto>> Submit([FromBody] SubmitGymCandidateRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await candidates.SubmitAsync(request, ct));
}
