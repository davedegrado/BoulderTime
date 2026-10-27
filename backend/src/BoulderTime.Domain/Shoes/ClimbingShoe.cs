using System.Globalization;
using System.Text;

namespace BoulderTime.Domain.Shoes;

public enum ShoeStatus { Current, Discontinued }

/// <summary>
/// A model of climbing shoe in the shared catalogue (ADR-070): brand and commercial name as the maker writes them,
/// whether still made, and — when there is one — the maker's picture and product page, linked, never copied. People
/// choose from the catalogue; only the catalogue's seed adds to it. A model is never deleted: people may have it.
/// </summary>
public class ClimbingShoe
{
    public Guid Id { get; private set; }
    public string Brand { get; private set; } = "";
    public string Name { get; private set; } = "";
    /// <summary>Brand and name folded (case, accents, spaces): one model once, however it is typed.</summary>
    public string Key { get; private set; } = "";
    /// <summary>What a search matches: brand and name folded the same way.</summary>
    public string SearchText { get; private set; } = "";
    public ShoeStatus Status { get; private set; }
    public string? ImageUrl { get; private set; }
    public string? ProductUrl { get; private set; }
    /// <summary>Where the name was checked; for maintenance only, never sent to the apps.</summary>
    public string? SourceUrl { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private ClimbingShoe() { }

    public static ClimbingShoe Create(string brand, string name, ShoeStatus status, string? imageUrl, string? productUrl, string? sourceUrl, DateTimeOffset now)
    {
        var shoe = new ClimbingShoe { Id = Guid.NewGuid(), Brand = Clean(brand), Name = Clean(name), CreatedAt = now };
        shoe.Key = KeyOf(shoe.Brand, shoe.Name);
        shoe.SearchText = Fold($"{shoe.Brand} {shoe.Name}");
        shoe.Update(status, imageUrl, productUrl, sourceUrl, now);
        return shoe;
    }

    /// <summary>Changes what may change about a model; brand and name never do (they are what people chose).</summary>
    public bool Update(ShoeStatus status, string? imageUrl, string? productUrl, string? sourceUrl, DateTimeOffset now)
    {
        var changed = Status != status || ImageUrl != imageUrl || ProductUrl != productUrl || SourceUrl != sourceUrl;
        Status = status; ImageUrl = imageUrl; ProductUrl = productUrl; SourceUrl = sourceUrl;
        if (changed) UpdatedAt = now;
        return changed;
    }

    public static string KeyOf(string brand, string name) => $"{Fold(brand)}|{Fold(name)}";

    /// <summary>Lower case, without accents, single spaces: "OCÚN  Ozone" and "ocun ozone" are the same.</summary>
    /// <remarks>
    /// Accents are taken off from a table, not with Unicode normalization: the API runs with invariant globalization,
    /// where normalization leaves "ú" as it is — and the key would then depend on how the process was started.
    /// </remarks>
    public static string Fold(string text)
    {
        var lower = Clean(text).ToLowerInvariant();
        var sb = new StringBuilder(lower.Length);
        foreach (var c in lower)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue; // a combining accent
            sb.Append(Plain.TryGetValue(c, out var plain) ? plain : c);
        }
        return sb.ToString();
    }

    private static readonly Dictionary<char, char> Plain = new[]
    {
        ('a', "àáâãäåāăą"), ('c', "çćĉċč"), ('d', "ďđ"), ('e', "èéêëēĕėęě"), ('g', "ĝğġģ"), ('h', "ĥħ"),
        ('i', "ìíîïĩīĭįı"), ('j', "ĵ"), ('k', "ķ"), ('l', "ĺļľŀł"), ('n', "ñńņňŉ"), ('o', "òóôõöøōŏő"),
        ('r', "ŕŗř"), ('s', "śŝşš"), ('t', "ţťŧ"), ('u', "ùúûüũūŭůűų"), ('w', "ŵ"), ('y', "ýÿŷ"), ('z', "źżž"),
    }.SelectMany(p => p.Item2.Select(c => (c, p.Item1))).ToDictionary(x => x.c, x => x.Item2);

    private static string Clean(string text) => string.Join(' ', (text ?? "").Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}

/// <summary>A pair someone climbs in, chosen from the catalogue; at most one is their main pair.</summary>
public class UserClimbingShoe
{
    public const int MaxPerPerson = 20;

    public Guid UserId { get; private set; }
    public Guid ShoeId { get; private set; }
    public bool IsPrimary { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private UserClimbingShoe() { }

    public static UserClimbingShoe Add(Guid userId, Guid shoeId, DateTimeOffset now) => new() { UserId = userId, ShoeId = shoeId, CreatedAt = now };

    public void SetPrimary(bool on) => IsPrimary = on;
}
