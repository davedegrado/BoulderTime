using BoulderTime.Application.Badges;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoulderTime.Api.Controllers;

/// <summary>Badges (ADR-069): names and descriptions in the caller's language (Accept-Language).</summary>
[ApiController]
[Route("api/users")]
[Authorize]
[Produces("application/json")]
public sealed class BadgesController(BadgeService badges) : ControllerBase
{
    private string Language => Request.Headers.AcceptLanguage.ToString();

    /// <summary>A person's badges and the ones in the foreground of their profile; nothing for a profile whose content is hidden.</summary>
    [HttpGet("{userId:guid}/badges")]
    [ProducesResponseType<UserBadgesDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public Task<UserBadgesDto> Get(Guid userId, CancellationToken ct) => badges.ForUserAsync(userId, Language, ct);

    /// <summary>Puts up to three of the caller's badges in the foreground of their profile, in this order.</summary>
    [HttpPut("me/badges/featured")]
    [ProducesResponseType<UserBadgesDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public Task<UserBadgesDto> SetFeatured([FromBody] SetFeaturedBadgesRequest request, CancellationToken ct) =>
        badges.SetFeaturedAsync(request, Language, ct);

    /// <summary>The caller's badges they haven't been shown yet.</summary>
    [HttpGet("me/badges/new")]
    [ProducesResponseType<IReadOnlyList<EarnedBadgeDto>>(StatusCodes.Status200OK)]
    public Task<IReadOnlyList<EarnedBadgeDto>> New(CancellationToken ct) => badges.NewForMeAsync(Language, ct);

    /// <summary>The caller has been shown their new badges.</summary>
    [HttpPost("me/badges/seen")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Seen(CancellationToken ct)
    {
        await badges.MarkSeenAsync(ct);
        return NoContent();
    }
}
