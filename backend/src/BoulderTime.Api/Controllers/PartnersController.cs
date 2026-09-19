using BoulderTime.Application.Gyms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoulderTime.Api.Controllers;

/// <summary>
/// Platform distinctions. Every endpoint here is restricted to BoulderTime administrators by the service layer:
/// gym owners, admins and staff cannot grant themselves the founding or early-partner status.
/// </summary>
[ApiController]
[Authorize]
[Route("api/admin")]
[Produces("application/json")]
public sealed class PartnersController(PartnerService partners) : ControllerBase
{
    /// <summary>The founding gym and the gyms currently in the early-partner programme.</summary>
    [HttpGet("partners")]
    public Task<IReadOnlyList<PartnerGymDto>> List(CancellationToken ct) => partners.ListAsync(ct);

    [HttpPut("gyms/{gymId:guid}/founding")]
    public Task<GymDetailDto> SetFounding(Guid gymId, [FromBody] SetFoundingGymRequest request, CancellationToken ct) =>
        partners.SetFoundingGymAsync(gymId, request, ct);

    [HttpPost("gyms/{gymId:guid}/early-partner")]
    public Task<EarlyPartnerDto> StartEarlyPartner(Guid gymId, [FromBody] StartEarlyPartnerRequest request, CancellationToken ct) =>
        partners.StartEarlyPartnerAsync(gymId, request, ct);

    [HttpDelete("gyms/{gymId:guid}/early-partner")]
    public async Task<IActionResult> EndEarlyPartner(Guid gymId, CancellationToken ct) =>
        await partners.EndEarlyPartnerAsync(gymId, ct) is { } ended ? Ok(ended) : NoContent();
}
