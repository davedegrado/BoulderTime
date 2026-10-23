using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Common;
using BoulderTime.Application.Gyms;
using BoulderTime.Domain.Apps;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BoulderTime.Application.Apps;

/// <param name="Minimum">Below this the app asks to be updated and lets nothing else through.</param>
/// <param name="Recommended">Below this the app suggests updating, and can be used anyway. Null: no suggestion.</param>
/// <param name="StoreUrl">The app's page on its store. Null: the app falls back to a store search.</param>
public sealed record AppPlatformVersionDto(string Minimum, string? Recommended, string? StoreUrl);

/// <summary>What the store apps check when they start and when they come back to the front. The website never does.</summary>
public sealed record AppVersionDto(AppPlatformVersionDto Ios, AppPlatformVersionDto Android);

public sealed record SetAppVersionRequest(string? Minimum, string? Recommended, string? StoreUrl);

/// <summary>
/// Read by every app start, so it is kept in memory for a short while (<see cref="CacheFor"/>). A change made here
/// replaces it at once on this server.
/// </summary>
public sealed class AppVersionCache
{
    private readonly object _gate = new();
    private (AppVersionDto Value, DateTimeOffset Until)? _entry;

    public AppVersionDto? Get(DateTimeOffset now)
    {
        lock (_gate) return _entry is { } e && e.Until > now ? e.Value : null;
    }

    public void Set(AppVersionDto value, DateTimeOffset until)
    {
        lock (_gate) _entry = (value, until);
    }

    public void Clear()
    {
        lock (_gate) _entry = null;
    }
}

/// <summary>
/// The minimum and recommended versions of the store apps (ADR-059). Kept in the database, not in the server's
/// settings, so they can be raised without publishing the API again: platform administrators change them.
/// </summary>
public sealed class AppVersionService(IAppDbContext db, AppVersionCache cache, GymAccess access, ICurrentUser currentUser, IClock clock,
    ILogger<AppVersionService> logger)
{
    public static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(60);

    /// <summary>The only places the update button may lead to.</summary>
    private static readonly string[] StoreHosts = ["apps.apple.com", "play.google.com"];

    public async Task<AppVersionDto> GetAsync(CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        if (cache.Get(now) is { } cached) return cached;
        var rows = await db.AppVersionPolicies.AsNoTracking().ToListAsync(ct);
        AppPlatformVersionDto Of(AppPlatform p) => rows.FirstOrDefault(r => r.Platform == p) is { } r
            ? new AppPlatformVersionDto(r.MinimumVersion, r.RecommendedVersion, r.StoreUrl)
            : new AppPlatformVersionDto("0.0.0", null, null); // no row: nobody is asked to update
        var value = new AppVersionDto(Of(AppPlatform.Ios), Of(AppPlatform.Android));
        cache.Set(value, now + CacheFor);
        return value;
    }

    public async Task<AppVersionDto> SetAsync(AppPlatform platform, SetAppVersionRequest r, CancellationToken ct = default)
    {
        await access.RequirePlatformAdminAsync(ct);
        var minimum = Input.Trimmed(r.Minimum);
        var recommended = Input.Trimmed(r.Recommended) is { Length: > 0 } rec ? rec : null;
        var storeUrl = Input.Trimmed(r.StoreUrl) is { Length: > 0 } url ? url : null;
        new Validator()
            .Check(AppVersionPolicy.Parse(minimum) is not null, "minimum", "Write the version as numbers and dots, for example 1.2.0.")
            .Check(recommended is null || AppVersionPolicy.Parse(recommended) is not null, "recommended", "Write the version as numbers and dots, for example 1.2.0.")
            .Check(recommended is null || AppVersionPolicy.Parse(minimum) is null || AppVersionPolicy.Compare(recommended, minimum) >= 0,
                "recommended", "The recommended version can't be older than the minimum.")
            .Check(storeUrl is null || IsStorePage(storeUrl), "storeUrl", "Use the app's https address on the App Store or Google Play.")
            .ThrowIfInvalid();

        var now = clock.UtcNow;
        var row = await db.AppVersionPolicies.FirstOrDefaultAsync(x => x.Platform == platform, ct);
        if (row is null) db.AppVersionPolicies.Add(AppVersionPolicy.Create(platform, minimum, recommended, storeUrl, now));
        else row.Update(minimum, recommended, storeUrl, currentUser.UserId, now);
        await db.SaveChangesAsync(ct);
        cache.Clear();
        logger.LogInformation("Platform admin {UserId} set the {Platform} app versions: minimum {Minimum}, recommended {Recommended}",
            currentUser.UserId, platform, minimum, recommended ?? "none");
        return await GetAsync(ct);
    }

    private static bool IsStorePage(string url) =>
        url.Length <= AppVersionPolicy.StoreUrlMaxLength
        && Uri.TryCreate(url, UriKind.Absolute, out var u) && u.Scheme == Uri.UriSchemeHttps && StoreHosts.Contains(u.Host);
}
