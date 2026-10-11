using BoulderTime.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoulderTime.Api.Controllers;

/// <summary>
/// Server settings the app has to follow, so it never offers what the server would refuse (ADR-049). Public: the help
/// pages read it before anyone signs in.
/// </summary>
[ApiController]
[Produces("application/json")]
public sealed class SettingsController(IVideoSettings videos) : ControllerBase
{
    [HttpGet("api/settings/videos"), AllowAnonymous]
    public VideoSettingsDto Videos() => new(videos.UploadsEnabled, videos.MaxUploadMegabytes);
}
