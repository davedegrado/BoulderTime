using BoulderTime.Domain.Common;

namespace BoulderTime.Domain.Gyms;

/// <summary>An area of a gym (e.g. "Cave", "Slab"). Deactivated rather than deleted once boulders reference it.</summary>
public class Sector : IAuditable
{
    public const int NameMaxLength = 60;
    public const int DescriptionMaxLength = 500;

    public Guid Id { get; private set; }
    public Guid GymId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? ImageUrl { get; private set; }
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

    public void SetActive(bool active) => IsActive = active;
    public void MoveTo(int sortOrder) => SortOrder = sortOrder;
}
