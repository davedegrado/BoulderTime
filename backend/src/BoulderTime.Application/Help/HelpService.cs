using System.Text.Json;
using System.Text.Json.Serialization;
using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Localization;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Help;

/// <summary>Who a question is for. Climbers only, gym staff only, or both.</summary>
public enum FaqAudience { Both = 0, Climbers = 1, Staff = 2 }

/// <param name="VideoUploads">Shown only while video uploads are on (true) or off (false); null: always.</param>
public sealed record FaqItemDto(string Q, string A,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] bool? VideoUploads = null);
public sealed record FaqSectionDto(string Id, string Title, IReadOnlyList<FaqItemDto> Items);

/// <summary>The guide as the viewer may read it. <see cref="Staff"/> is empty for anyone who isn't gym staff.</summary>
public sealed record FaqGuideDto(IReadOnlyList<FaqSectionDto> Climbers, IReadOnlyList<FaqSectionDto> Staff);

/// <summary>
/// The in-app help, served by the API so who reads what is decided here and not in the app: the staff answers are
/// not in the app's code at all, so hiding them is not a matter of a missing button. Each question says who it is for
/// (<see cref="FaqAudience"/>); gym staff are people with a role at any gym, and BoulderTime's administrators.
/// The texts are written once per language in <c>Help/faq-{language}.json</c>, embedded in this assembly.
/// </summary>
public sealed class HelpService(IAppDbContext db, ICurrentUser currentUser)
{
    private sealed record StoredItem(string Q, string A, FaqAudience Audience = FaqAudience.Both, bool? VideoUploads = null);
    private sealed record StoredSection(string Id, string Title, IReadOnlyList<StoredItem> Items);
    private sealed record StoredGuide(IReadOnlyList<StoredSection> Climbers, IReadOnlyList<StoredSection> Staff);

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private static readonly Lazy<Dictionary<string, StoredGuide>> Guides = new(() =>
        Language.Supported.ToDictionary(l => l, Load));

    private static StoredGuide Load(string language)
    {
        // Found by the end of its name, so the folder's part of the resource name can't get in the way.
        var assembly = typeof(HelpService).Assembly;
        var file = $"faq-{language}.json";
        var name = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith("." + file, StringComparison.Ordinal))
                   ?? throw new InvalidOperationException($"The help texts are missing ({file}).");
        using var stream = assembly.GetManifestResourceStream(name)
                           ?? throw new InvalidOperationException($"The help texts are missing ({name}).");
        return JsonSerializer.Deserialize<StoredGuide>(stream, Json) ?? throw new InvalidOperationException($"The help texts are empty ({name}).");
    }

    public static bool CanRead(FaqAudience audience, bool isStaff) => audience switch
    {
        FaqAudience.Staff => isStaff,
        FaqAudience.Climbers => !isStaff,
        _ => true,
    };

    public async Task<FaqGuideDto> GuideAsync(string? language, CancellationToken ct = default)
    {
        var isStaff = await IsStaffAsync(ct);
        var guide = Guides.Value[Language.Normalize(language)];
        return new FaqGuideDto(Filter(guide.Climbers, isStaff), isStaff ? Filter(guide.Staff, isStaff) : []);
    }

    private static List<FaqSectionDto> Filter(IReadOnlyList<StoredSection> sections, bool isStaff) => sections
        .Select(s => new FaqSectionDto(s.Id, s.Title, s.Items.Where(i => CanRead(i.Audience, isStaff)).Select(i => new FaqItemDto(i.Q, i.A, i.VideoUploads)).ToList()))
        .Where(s => s.Items.Count > 0)
        .ToList();

    private async Task<bool> IsStaffAsync(CancellationToken ct)
    {
        if (currentUser.UserId is not { } id) return false;
        return await db.GymStaff.AnyAsync(s => s.UserId == id, ct) || await db.Users.AnyAsync(u => u.Id == id && u.IsPlatformAdmin, ct);
    }
}
