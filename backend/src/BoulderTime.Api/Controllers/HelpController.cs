using BoulderTime.Application.Help;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoulderTime.Api.Controllers;

/// <summary>The in-app help. Readable without signing in; the staff guide only comes back for gym staff.</summary>
[ApiController]
[Produces("application/json")]
public sealed class HelpController(HelpService help) : ControllerBase
{
    /// <param name="lang">"it" or "en"; anything else falls back to Italian.</param>
    [HttpGet("api/help/faq"), AllowAnonymous]
    public Task<FaqGuideDto> Faq([FromQuery] string? lang, CancellationToken ct) => help.GuideAsync(lang, ct);
}
