namespace BoulderTime.Domain.Apps;

/// <summary>A store app: the two are published separately, so each has its own versions.</summary>
public enum AppPlatform
{
    Ios,
    Android,
}

/// <summary>
/// Which versions of a store app may still be used. Below <see cref="MinimumVersion"/> the app asks to be updated and
/// lets nothing else through; below <see cref="RecommendedVersion"/> it only suggests it. The website is always the
/// latest and never asks. Changed by platform administrators, without publishing the API again (ADR-059).
/// </summary>
public sealed class AppVersionPolicy
{
    public const int VersionMaxLength = 32;
    public const int StoreUrlMaxLength = 300;

    public AppPlatform Platform { get; private set; }
    public string MinimumVersion { get; private set; } = "1.0.0";
    public string? RecommendedVersion { get; private set; }
    /// <summary>The app's page on the App Store or Google Play, which the update button opens.</summary>
    public string? StoreUrl { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid? UpdatedByUserId { get; private set; }

    private AppVersionPolicy() { }

    public static AppVersionPolicy Create(AppPlatform platform, string minimum, string? recommended, string? storeUrl, DateTimeOffset now) =>
        new() { Platform = platform, MinimumVersion = minimum, RecommendedVersion = recommended, StoreUrl = storeUrl, UpdatedAt = now };

    public void Update(string minimum, string? recommended, string? storeUrl, Guid? byUserId, DateTimeOffset now)
    {
        MinimumVersion = minimum;
        RecommendedVersion = recommended;
        StoreUrl = storeUrl;
        UpdatedByUserId = byUserId;
        UpdatedAt = now;
    }

    /// <summary>
    /// Compares two versions part by part as numbers, so 1.10.0 is newer than 1.9.0; missing parts count as 0
    /// (1.2 is 1.2.0). Null when either is not a version of the form 1, 1.2 or 1.2.3.
    /// </summary>
    public static int? Compare(string a, string b)
    {
        if (Parse(a) is not { } x || Parse(b) is not { } y) return null;
        for (var i = 0; i < 3; i++)
            if (x[i] != y[i]) return x[i].CompareTo(y[i]);
        return 0;
    }

    /// <summary>The three numbers of a version of up to three parts, or null if it isn't one.</summary>
    public static int[]? Parse(string? version)
    {
        var parts = (version ?? "").Trim().Split('.');
        if (parts.Length is < 1 or > 3) return null;
        var numbers = new int[3];
        for (var i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length is 0 or > 5 || !parts[i].All(char.IsAsciiDigit)) return null;
            numbers[i] = int.Parse(parts[i]);
        }
        return numbers;
    }
}
