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

    /// <summary>Social profiles, both optional: most gyms post their resets on Instagram rather than on a website.</summary>
    public string? InstagramUrl { get; private set; }
    public string? FacebookUrl { get; private set; }
    public string? LogoUrl { get; private set; }
    public string? LogoPath { get; private set; }
    public string? CoverImageUrl { get; private set; }
    public string? CoverImagePath { get; private set; }
    /// <summary>WGS84 position for the map. Optional: gyms without coordinates don't appear as pins.</summary>
    public double? Latitude { get; private set; }
    public double? Longitude { get; private set; }
    /// <summary>
    /// The one gym that launched BoulderTime with us. A historical distinction, not a commercial tier:
    /// at most one gym can hold it (guaranteed by a partial unique index) and only platform admins can move it.
    /// </summary>
    public bool IsFoundingGym { get; private set; }
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

    public void UpdateProfile(string name, string? description, string? address, string city, string? website, string? email, string? phone,
        string? instagramUrl = null, string? facebookUrl = null)
    {
        Name = name.Trim();
        Description = Clean(description);
        Address = Clean(address);
        City = city.Trim();
        Website = Clean(website);
        Email = Clean(email)?.ToLowerInvariant();
        Phone = Clean(phone);
        InstagramUrl = SocialUrl(instagramUrl, "instagram.com");
        FacebookUrl = SocialUrl(facebookUrl, "facebook.com");
    }

    /// <summary>
    /// Accepts what gyms actually paste: a full link, or just the handle. Anything that is not a link to that
    /// network is refused, so the profile can't be used to point climbers somewhere else.
    /// </summary>
    private static string? SocialUrl(string? value, string host)
    {
        var text = Clean(value);
        if (text is null) return null;
        if (text.StartsWith('@')) text = text[1..];
        if (!text.Contains('/') && !text.Contains(' ') && !text.Contains('.'))
            return $"https://{host}/{text}";
        if (!text.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !text.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            text = "https://" + text;
        if (!Uri.TryCreate(text, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            throw new ArgumentException($"Use a link to {host}.", nameof(value));
        var domain = uri.Host.StartsWith("www.", StringComparison.OrdinalIgnoreCase) ? uri.Host[4..] : uri.Host;
        if (!domain.Equals(host, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Use a link to {host}.", nameof(value));
        return uri.ToString();
    }

    /// <returns>The replaced storage path, if any, so the old file can be deleted.</returns>
    public string? SetLogo(string? url, string? path)
    {
        var previous = LogoPath != path ? LogoPath : null;
        LogoUrl = Clean(url);
        LogoPath = Clean(path);
        return previous;
    }

    /// <returns>The replaced storage path, if any, so the old file can be deleted.</returns>
    public string? SetCover(string? url, string? path)
    {
        var previous = CoverImagePath != path ? CoverImagePath : null;
        CoverImageUrl = Clean(url);
        CoverImagePath = Clean(path);
        return previous;
    }

    public void SetStatus(GymStatus status) => Status = status;

    public void SetFoundingGym(bool isFounding) => IsFoundingGym = isFounding;

    /// <summary>Both values or neither.</summary>
    public void SetLocation(double? latitude, double? longitude)
    {
        if ((latitude is null) != (longitude is null)) throw new ArgumentException("Latitude and longitude go together.");
        if (latitude is < -90 or > 90) throw new ArgumentOutOfRangeException(nameof(latitude));
        if (longitude is < -180 or > 180) throw new ArgumentOutOfRangeException(nameof(longitude));
        Latitude = latitude is null ? null : Math.Round(latitude.Value, 6);
        Longitude = longitude is null ? null : Math.Round(longitude.Value, 6);
    }

    public bool IsPubliclyVisible => Status == GymStatus.Active;

    private static string? Clean(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
