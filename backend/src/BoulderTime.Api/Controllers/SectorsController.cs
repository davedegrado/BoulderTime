using BoulderTime.Application.Gyms;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoulderTime.Api.Controllers;

[ApiController]
[Route("api/sectors")]
[Authorize]
[Produces("application/json")]
public sealed class SectorsController(SectorService sectors) : ControllerBase
{
    [HttpPatch("{sectorId:guid}")]
    public Task<SectorDto> Update(Guid sectorId, [FromBody] UpdateSectorRequest request, CancellationToken ct) =>
        sectors.UpdateAsync(sectorId, request, ct);
}
