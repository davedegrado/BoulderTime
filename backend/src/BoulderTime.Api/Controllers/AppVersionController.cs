using BoulderTime.Api.Security;
using BoulderTime.Application.Apps;
using BoulderTime.Domain.Apps;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BoulderTime.Api.Controllers;

/// <summary>
/// The store apps' minimum and recommended versions (ADR-059). Reading is public: the app checks before anyone signs
/// in. Each answer may be kept for a minute, by the server and by whatever sits in between.
/// </summary>
[ApiController]
[Produces("application/json")]
public sealed class AppVersionController(AppVersionService versions) : ControllerBase
{
    [HttpGet("api/app-version"), AllowAnonymous]
    public async Task<AppVersionDto> Get(CancellationToken ct)
    {
        Response.Headers.CacheControl = $"public, max-age={(int)AppVersionService.CacheFor.TotalSeconds}";
        return await versions.GetAsync(ct);
    }

    /// <summary>Platform administrators only. Raise the minimum only once the new version is on the store.</summary>
    [HttpPut("api/admin/app-versions/{platform}"), Authorize, EnableRateLimiting(RateLimiting.WritePolicy)]
    public Task<AppVersionDto> Set(AppPlatform platform, [FromBody] SetAppVersionRequest request, CancellationToken ct) =>
        versions.SetAsync(platform, request, ct);
}
