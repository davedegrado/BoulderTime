namespace BoulderTime.Application.Localization;

/// <summary>The languages BoulderTime speaks. Italian is the default: the platform launches in Italy.</summary>
public static class Language
{
    public const string Italian = "it";
    public const string English = "en";
    public const string Default = Italian;

    public static readonly string[] Supported = [Italian, English];

    /// <summary>Accepts "it", "IT", "it-IT", an Accept-Language header, or anything else (falls back to the default).</summary>
    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return Default;
        foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var tag = part.Split(';')[0].Trim().ToLowerInvariant();
            if (tag.StartsWith("it")) return Italian;
            if (tag.StartsWith("en")) return English;
        }
        return Default;
    }

    public static bool IsSupported(string? value) => value is not null && Supported.Contains(value.Trim().ToLowerInvariant());
}
