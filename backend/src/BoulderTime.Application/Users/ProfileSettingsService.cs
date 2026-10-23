using System.Text.Json;
using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Common;
using BoulderTime.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Users;

/// <summary>One section of the public profile, in the order it is shown.</summary>
public sealed record ProfileSectionDto(string Key, bool Visible);

public sealed record ProfilePrivacyRequest(bool? Private);

public sealed record ProfileSectionsRequest(IReadOnlyList<ProfileSectionDto>? Sections);

/// <summary>
/// The sections a public profile can show, in their default order. Built from what the profile really has; a section
/// the person hides is only left out of the page, its data stays as it is (privacy is <see cref="ProfileVisibility"/>).
/// </summary>
public static class ProfileLayout
{
    public static readonly string[] Keys = ["stats", "favoriteSector", "gyms", "weekly", "highestGrades", "history"];

    public static IReadOnlyList<ProfileSectionDto> Default { get; } = Keys.Select(k => new ProfileSectionDto(k, true)).ToList();

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>
    /// The arrangement to show: the saved one, with unknown keys dropped and sections added later appended, visible,
    /// so a profile arranged today still shows what comes tomorrow. Anything unreadable gives the default.
    /// </summary>
    public static IReadOnlyList<ProfileSectionDto> Read(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Default;
        List<ProfileSectionDto>? saved;
        try { saved = JsonSerializer.Deserialize<List<ProfileSectionDto>>(json, Json); }
        catch (JsonException) { return Default; }
        var result = new List<ProfileSectionDto>();
        foreach (var s in saved ?? [])
            if (s is not null && Keys.Contains(s.Key) && result.All(r => r.Key != s.Key)) result.Add(s);
        result.AddRange(Keys.Where(k => result.All(r => r.Key != k)).Select(k => new ProfileSectionDto(k, true)));
        return result;
    }

    public static string Write(IReadOnlyList<ProfileSectionDto> sections) => JsonSerializer.Serialize(sections, Json);
}

/// <summary>The person's own profile settings: who sees its content, and how its sections are arranged (ADR-061).</summary>
public sealed class ProfileSettingsService(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task SetPrivacyAsync(ProfilePrivacyRequest r, CancellationToken ct = default)
    {
        if (r.Private is not { } isPrivate) throw new ValidationException("private", "Say whether the profile is private.");
        var user = await MeAsync(ct);
        user.SetProfileVisibility(isPrivate ? ProfileVisibility.Private : ProfileVisibility.Public);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ProfileSectionDto>> SectionsAsync(CancellationToken ct = default) =>
        ProfileLayout.Read((await MeAsync(ct)).ProfileSections);

    public async Task<IReadOnlyList<ProfileSectionDto>> SetSectionsAsync(ProfileSectionsRequest r, CancellationToken ct = default)
    {
        var sections = r.Sections ?? [];
        var keys = sections.Select(s => s?.Key).ToList();
        new Validator()
            .Check(sections.Count > 0 && keys.All(k => k is not null && ProfileLayout.Keys.Contains(k)), "sections", "Unknown profile section.")
            .Check(keys.Distinct().Count() == keys.Count, "sections", "Each section can appear only once.")
            .ThrowIfInvalid();
        var user = await MeAsync(ct);
        var layout = ProfileLayout.Read(ProfileLayout.Write(sections));
        user.SetProfileSections(ProfileLayout.Write(layout));
        await db.SaveChangesAsync(ct);
        return layout;
    }

    public async Task<IReadOnlyList<ProfileSectionDto>> ResetSectionsAsync(CancellationToken ct = default)
    {
        var user = await MeAsync(ct);
        user.SetProfileSections(null);
        await db.SaveChangesAsync(ct);
        return ProfileLayout.Default;
    }

    private async Task<User> MeAsync(CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();
        return await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct) ?? throw new NotFoundException("User", userId);
    }
}
