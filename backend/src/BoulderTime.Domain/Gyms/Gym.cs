using BoulderTime.Domain.Common;

namespace BoulderTime.Domain.Gyms;

/// <summary>
/// A bouldering gym. Gyms are created only by BoulderTime platform admins (usually from a <see cref="Candidates.GymCandidate"/>);
/// users can never create a gym and make themselves staff.
/// </summary>
public class Gym : IAuditable
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 2000;
    public const int CityMaxLength = 100;
    public const int AddressMaxLength = 200;
    public const int UrlMaxLength = 2048;
    public const int EmailMaxLength = 320;
    public const int PhoneMaxLength = 40;

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    /// <summary>URL identifier, unique across the platform. Stable once created.</summary>
    public string Slug { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? Address { get; private set; }
    public string City { get; private set; } = string.Empty;
    public string? Website { get; private set; }
    public string? Email { get; private set; }
    public string? Phone { get; private set; }
    public string? LogoUrl { get; private set; }
    public string? CoverImageUrl { get; private set; }
    public GymStatus Status { get; private set; } = GymStatus.Draft;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    private Gym() { }

    public static Gym Create(string name, string slug, string city)
    {
        var gym = new Gym { Id = Guid.NewGuid(), Slug = slug, Status = GymStatus.Draft };
        gym.UpdateProfile(name, null, null, city, null, null, null);
        return gym;
    }

    public void UpdateProfile(string name, string? description, string? address, string city, string? website, string? email, string? phone)
    {
        Name = name.Trim();
        Description = Clean(description);
        Address = Clean(address);
        City = city.Trim();
        Website = Clean(website);
        Email = Clean(email)?.ToLowerInvariant();
        Phone = Clean(phone);
    }

    public void SetImages(string? logoUrl, string? coverImageUrl)
    {
        LogoUrl = Clean(logoUrl);
        CoverImageUrl = Clean(coverImageUrl);
    }

    public void SetStatus(GymStatus status) => Status = status;

    public bool IsPubliclyVisible => Status == GymStatus.Active;

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
