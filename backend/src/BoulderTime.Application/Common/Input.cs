using System.Text.RegularExpressions;

namespace BoulderTime.Application.Common;

/// <summary>Shared input checks used by request validation.</summary>
public static partial class Input
{
    [GeneratedRegex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$")]
    private static partial Regex EmailRegex();

    public static string Trimmed(string? value) => (value ?? string.Empty).Trim();

    public static bool IsEmail(string? value) =>
        !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 320 && EmailRegex().IsMatch(value.Trim());

    public static bool IsHttpUrl(string? value) =>
        Uri.TryCreate(value?.Trim(), UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    public static bool OptionalEmail(string? value) => string.IsNullOrWhiteSpace(value) || IsEmail(value);
    public static bool OptionalUrl(string? value) => string.IsNullOrWhiteSpace(value) || IsHttpUrl(value);
    public static bool MaxLength(string? value, int max) => (value?.Trim().Length ?? 0) <= max;

    /// <summary>Escapes LIKE wildcards so user search text is matched literally.</summary>
    public static string LikeContains(string value) =>
        "%" + value.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_") + "%";
}
