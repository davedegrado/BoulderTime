using System.Globalization;
using System.Text;

namespace BoulderTime.Domain.Boulders;

/// <summary>
/// A label a gym puts on its boulders ("Placca", "Dinamico", "Resistenza"…). Each gym keeps its own list: there is
/// no global vocabulary, because gyms describe their walls in their own words. Boulders point at the tag, never copy
/// its text, so renaming a tag renames it everywhere and deleting it leaves nothing behind.
/// </summary>
public class GymTag
{
    public const int NameMaxLength = 24;
    public const int MaxPerGym = 50;
    public const int MaxPerBoulder = 6;

    public Guid Id { get; private set; }
    public Guid GymId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    /// <summary>The name as compared for duplicates: "Placca", "placca " and "Placcà" are one tag (see <see cref="Normalize"/>).</summary>
    public string NormalizedName { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; private set; }

    private GymTag() { }

    public static GymTag Create(Guid gymId, string name, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), GymId = gymId, Name = Clean(name), NormalizedName = Normalize(name), CreatedAt = now,
    };

    public void Rename(string name)
    {
        Name = Clean(name);
        NormalizedName = Normalize(name);
    }

    /// <summary>Trimmed, inner runs of spaces folded to one. What people see.</summary>
    public static string Clean(string name) => string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));

    /// <summary>
    /// What two names must share to be the same tag: case, accents and spacing don't count. Stored, so the database
    /// itself refuses a duplicate even when two people create one at the same moment.
    /// </summary>
    public static string Normalize(string name)
    {
        var decomposed = Clean(name).ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var plain = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) plain.Append(c);
        return plain.ToString().Normalize(NormalizationForm.FormC);
    }
}

/// <summary>A tag on a boulder. Goes with the tag when the gym deletes it.</summary>
public class BoulderTag
{
    public Guid BoulderId { get; private set; }
    public Guid TagId { get; private set; }

    private BoulderTag() { }

    public static BoulderTag Create(Guid boulderId, Guid tagId) => new() { BoulderId = boulderId, TagId = tagId };
}
