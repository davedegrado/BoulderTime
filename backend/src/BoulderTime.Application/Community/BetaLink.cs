namespace BoulderTime.Application.Community;

/// <summary>
/// Where a linked beta may point. Only video sites gyms actually publish on, and only https: the address is handed
/// to every climber's browser, so an open field here would let staff — or anyone who got hold of a staff account —
/// send the whole gym to a page of their choosing.
/// </summary>
public static class BetaLink
{
    private static readonly string[] Hosts =
    [
        "youtube.com", "youtu.be", "instagram.com", "vimeo.com", "tiktok.com", "facebook.com", "fb.watch",
    ];

    /// <summary>The list as the climber reads it, for error messages and for the form's hint.</summary>
    public static string Accepted => "YouTube, Instagram, Vimeo, TikTok, Facebook";

    /// <summary>
    /// The address to store, or null if it is not one we accept. Adds the scheme when it is missing (people paste
    /// "instagram.com/p/…"), keeps the query string — a YouTube link IS its "?v=" — drops any fragment, and matches
    /// the host on its own labels, so youtube.com.example.org is refused rather than read as YouTube.
    /// </summary>
    public static string? Normalise(string? raw)
    {
        var text = raw?.Trim();
        if (string.IsNullOrEmpty(text)) return null;
        if (!text.Contains("://", StringComparison.Ordinal)) text = "https://" + text;
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri)) return null;
        if (uri.Scheme != Uri.UriSchemeHttps) return null;

        var host = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host;
        var known = Hosts.Any(h =>
            host.Equals(h, StringComparison.OrdinalIgnoreCase) ||
            host.EndsWith("." + h, StringComparison.OrdinalIgnoreCase));
        if (!known) return null;
        if (uri.AbsolutePath.Length <= 1) return null; // the site's front page is not a beta

        var url = uri.GetLeftPart(UriPartial.Query);
        return url.Length <= Domain.Community.BoulderBeta.ExternalUrlMaxLength ? url : null;
    }
}
