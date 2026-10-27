using BoulderTime.Application.Shoes;
using BoulderTime.Api.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BoulderTime.Api.Controllers;

/// <summary>Climbing shoes (ADR-070): the shared catalogue, read only, and each person's pairs.</summary>
[ApiController]
[Authorize]
[Produces("application/json")]
public sealed class ShoesController(ShoeService shoes) : ControllerBase
{
    /// <summary>A page of the catalogue, by words of the model or brand and optionally one brand.</summary>
    [HttpGet("api/climbing-shoes")]
    public Task<ShoeCatalogPageDto> Search([FromQuery] string? q, [FromQuery] string? brand, [FromQuery] int? limit, CancellationToken ct) =>
        shoes.SearchAsync(q, brand, limit, ct);

    /// <summary>A person's shoes, the main pair first; nothing for a profile whose content is hidden.</summary>
    [HttpGet("api/users/{userId:guid}/shoes")]
    public Task<UserShoesDto> Get(Guid userId, CancellationToken ct) => shoes.ForUserAsync(userId, ct);

    [HttpPut("api/users/me/shoes/{shoeId:guid}"), EnableRateLimiting(RateLimiting.WritePolicy)]
    public Task<UserShoesDto> Add(Guid shoeId, CancellationToken ct) => shoes.AddAsync(shoeId, ct);

    [HttpDelete("api/users/me/shoes/{shoeId:guid}"), EnableRateLimiting(RateLimiting.WritePolicy)]
    public Task<UserShoesDto> Remove(Guid shoeId, CancellationToken ct) => shoes.RemoveAsync(shoeId, ct);

    [HttpPut("api/users/me/shoes/{shoeId:guid}/primary"), EnableRateLimiting(RateLimiting.WritePolicy)]
    public Task<UserShoesDto> MakePrimary(Guid shoeId, CancellationToken ct) => shoes.SetPrimaryAsync(shoeId, true, ct);

    [HttpDelete("api/users/me/shoes/{shoeId:guid}/primary"), EnableRateLimiting(RateLimiting.WritePolicy)]
    public Task<UserShoesDto> NotPrimary(Guid shoeId, CancellationToken ct) => shoes.SetPrimaryAsync(shoeId, false, ct);
}
