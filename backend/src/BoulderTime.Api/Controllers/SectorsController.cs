using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Gyms;
using BoulderTime.Application.Images;
using BoulderTime.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

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

    [HttpPut("{sectorId:guid}/zone")]
    public Task<SectorDto> SetZone(Guid sectorId, [FromBody] SetSectorZoneRequest request, CancellationToken ct) =>
        sectors.SetZoneAsync(sectorId, request, ct);

    [HttpPost("{sectorId:guid}/photo-uploads"), EnableRateLimiting(RateLimiting.WritePolicy)]
    public Task<UploadTicket> PhotoUpload(Guid sectorId, [FromBody] ImageUploadRequest request, CancellationToken ct) =>
        sectors.CreatePhotoUploadAsync(sectorId, request, ct);

    /// <summary>Sets, replaces or (no path) removes the wall photo. Boulders already set on the old one keep it.</summary>
    [HttpPut("{sectorId:guid}/photo")]
    public Task<SectorDto> SetPhoto(Guid sectorId, [FromBody] SetImageRequest request, CancellationToken ct) =>
        sectors.SetPhotoAsync(sectorId, request, ct);

    [HttpGet("{sectorId:guid}/photo")]
    public async Task<IActionResult> Photo(Guid sectorId, CancellationToken ct)
    {
        var (content, contentType) = await sectors.PhotoAsync(sectorId, ct);
        return File(content, contentType);
    }
}
