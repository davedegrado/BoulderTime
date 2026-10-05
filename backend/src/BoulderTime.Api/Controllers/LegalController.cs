using BoulderTime.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoulderTime.Api.Controllers;

/// <summary>Recording that someone accepted the terms and the privacy notice.</summary>
[ApiController]
[Authorize]
[Produces("application/json")]
public sealed class LegalController(LegalAcceptanceService service) : ControllerBase
{
    [HttpPost("api/users/me/legal-acceptance")]
    public Task<LegalAcceptanceDto> Accept([FromBody] AcceptLegalRequest request, CancellationToken ct) =>
        service.AcceptAsync(request, ct);
}
