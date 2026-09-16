using System.Globalization;
using System.Text;

namespace BoulderTime.Domain.Common;

public static class Slug
{
    public const int MaxLength = 80;

    /// <summary>"Boulder Café Milano!" → "boulder-cafe-milano".</summary>
    public static string From(string value)
    {
        var normalized = (value ?? string.Empty).Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        var lastDash = true;
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            var c = char.ToLowerInvariant(ch);
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9')
            {
                sb.Append(c);
                lastDash = false;
            }
            else if (!lastDash)
            {
                sb.Append('-');
                lastDash = true;
            }
        }
        var slug = sb.ToString().Trim('-');
        if (slug.Length > MaxLength) slug = slug[..MaxLength].TrimEnd('-');
        return slug.Length == 0 ? "gym" : slug;
    }
}
