using BoulderTime.Domain.Common;

namespace BoulderTime.Domain.Gyms;

/// <summary>An area of a gym (e.g. "Cave", "Slab"). Deactivated rather than deleted once boulders reference it.</summary>
public class Sector : IAuditable
{
    public const int NameMaxLength = 60;
    public const int DescriptionMaxLength = 500;
    public const int MapZoneMaxLength = 4000;

    public Guid Id { get; private set; }
    public Guid GymId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? ImageUrl { get; private set; }
    /// <summary>
    /// The wall photo in the boulder-images bucket (gyms/{gymId}/sectors/…). Boulders set on this wall can use it as
    /// their own photo, each with its own card picture; the file belongs to the sector, never to a boulder.
    /// </summary>
    public string? PhotoPath { get; private set; }
    /// <summary>
    /// The sector's area on the gym's floor plan, as JSON: its outline and where its label sits, in fractions of the
    /// plan's width and height (0–1), so it fits the plan at any size. Null when it isn't drawn.
    /// </summary>
    public string? MapZone { get; private set; }
    public int SortOrder { get; private set; }
    public bool IsActive { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    private Sector() { }

    public static Sector Create(Guid gymId, string name, string? description, int sortOrder) => new()
    {
        Id = Guid.NewGuid(),
        GymId = gymId,
        Name = name.Trim(),
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
        SortOrder = sortOrder,
    };

    public void Update(string name, string? description)
    {
        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
    }

    /// <summary>Where the sectors' wall photos live: next to the boulders' photos, in a folder no boulder owns.</summary>
    public static string PhotoPrefix(Guid gymId) => $"gyms/{gymId}/sectors/";

    /// <summary>Whether a photo path is a sector's wall photo, which boulders only borrow.</summary>
    public static bool IsSectorPhoto(string? path) => path is not null && path.Contains("/sectors/", StringComparison.Ordinal);

    /// <returns>The replaced storage path, if any, so the old file can be deleted.</returns>
    public string? SetPhoto(string? url, string? path)
    {
        var previous = PhotoPath != path ? PhotoPath : null;
        ImageUrl = string.IsNullOrWhiteSpace(url) ? null : url.Trim();
        PhotoPath = string.IsNullOrWhiteSpace(path) ? null : path.Trim();
        return previous;
    }

    public void SetActive(bool active) => IsActive = active;
    public void SetMapZone(string? json) => MapZone = json;
    public void MoveTo(int sortOrder) => SortOrder = sortOrder;
}
