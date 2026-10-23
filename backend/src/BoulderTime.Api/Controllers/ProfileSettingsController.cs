using BoulderTime.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BoulderTime.Api.Controllers;

/// <summary>
/// The signed-in person's profile settings (ADR-061): whether its content is private, and which sections it shows in
/// which order. New routes only: apps that don't know them keep working as before.
/// </summary>
[ApiController]
[Authorize]
[Produces("application/json")]
public sealed class ProfileSettingsController(ProfileSettingsService settings) : ControllerBase
{
    [HttpPut("api/users/me/profile-privacy")]
    public async Task<IActionResult> SetPrivacy([FromBody] ProfilePrivacyRequest request, CancellationToken ct)
    {
        await settings.SetPrivacyAsync(request, ct);
        return NoContent();
    }

    [HttpGet("api/users/me/profile-sections")]
    public Task<IReadOnlyList<ProfileSectionDto>> Sections(CancellationToken ct) => settings.SectionsAsync(ct);

    [HttpPut("api/users/me/profile-sections")]
    public Task<IReadOnlyList<ProfileSectionDto>> SetSections([FromBody] ProfileSectionsRequest request, CancellationToken ct) =>
        settings.SetSectionsAsync(request, ct);

    /// <summary>Back to the default arrangement.</summary>
    [HttpDelete("api/users/me/profile-sections")]
    public Task<IReadOnlyList<ProfileSectionDto>> ResetSections(CancellationToken ct) => settings.ResetSectionsAsync(ct);
}
