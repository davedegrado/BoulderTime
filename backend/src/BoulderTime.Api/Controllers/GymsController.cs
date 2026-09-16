using BoulderTime.Application.Common;
using BoulderTime.Application.Gyms;
using BoulderTime.Application.Staff;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoulderTime.Api.Controllers;

/// <summary>Public gym discovery plus gym-scoped management. Authorization is enforced in the application services.</summary>
[ApiController]
[Route("api/gyms")]
[Produces("application/json")]
public sealed class GymsController(GymService gyms, SectorService sectors, StaffService staff) : ControllerBase
{
    [HttpGet, AllowAnonymous]
    public Task<PagedResult<GymSummaryDto>> Search([FromQuery] string? q, [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken ct) =>
        gyms.SearchAsync(q, page, pageSize, ct);

    [HttpGet("{slug}"), AllowAnonymous]
    public Task<GymDetailDto> GetBySlug(string slug, CancellationToken ct) => gyms.GetBySlugAsync(slug, ct);

    [HttpPatch("{gymId:guid}"), Authorize]
    public Task<GymDetailDto> Update(Guid gymId, [FromBody] UpdateGymRequest request, CancellationToken ct) =>
        gyms.UpdateAsync(gymId, request, ct);

    // ---- Sectors ----

    [HttpGet("{gymId:guid}/sectors"), AllowAnonymous]
    public Task<IReadOnlyList<SectorDto>> ListSectors(Guid gymId, CancellationToken ct) => sectors.ListAsync(gymId, ct);

    [HttpPost("{gymId:guid}/sectors"), Authorize]
    public async Task<ActionResult<SectorDto>> CreateSector(Guid gymId, [FromBody] CreateSectorRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await sectors.CreateAsync(gymId, request, ct));

    [HttpPut("{gymId:guid}/sectors/order"), Authorize]
    public Task<IReadOnlyList<SectorDto>> ReorderSectors(Guid gymId, [FromBody] ReorderSectorsRequest request, CancellationToken ct) =>
        sectors.ReorderAsync(gymId, request, ct);

    // ---- Staff ----

    [HttpGet("{gymId:guid}/staff"), Authorize]
    public Task<IReadOnlyList<StaffMemberDto>> ListStaff(Guid gymId, CancellationToken ct) => staff.ListMembersAsync(gymId, ct);

    [HttpPatch("{gymId:guid}/staff/{userId:guid}"), Authorize]
    public Task<StaffMemberDto> ChangeRole(Guid gymId, Guid userId, [FromBody] ChangeRoleRequest request, CancellationToken ct) =>
        staff.ChangeRoleAsync(gymId, userId, request, ct);

    [HttpDelete("{gymId:guid}/staff/{userId:guid}"), Authorize]
    public async Task<IActionResult> RemoveStaff(Guid gymId, Guid userId, CancellationToken ct)
    {
        await staff.RemoveAsync(gymId, userId, ct);
        return NoContent();
    }

    [HttpGet("{gymId:guid}/invitations"), Authorize]
    public Task<IReadOnlyList<GymInvitationDto>> ListInvitations(Guid gymId, CancellationToken ct) => staff.ListPendingInvitationsAsync(gymId, ct);

    [HttpPost("{gymId:guid}/invitations"), Authorize]
    public async Task<ActionResult<GymInvitationDto>> Invite(Guid gymId, [FromBody] InviteStaffRequest request, CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await staff.InviteAsync(gymId, request, ct));

    [HttpDelete("{gymId:guid}/invitations/{invitationId:guid}"), Authorize]
    public async Task<IActionResult> RevokeInvitation(Guid gymId, Guid invitationId, CancellationToken ct)
    {
        await staff.RevokeInvitationAsync(gymId, invitationId, ct);
        return NoContent();
    }
}
