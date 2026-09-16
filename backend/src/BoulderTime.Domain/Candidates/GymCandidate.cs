namespace BoulderTime.Domain.Candidates;

public enum GymCandidateStatus
{
    Pending = 0,
    Contacted = 1,
    Accepted = 2,
    Rejected = 3,
}

/// <summary>A user's suggestion that BoulderTime should onboard a gym. Handled by platform admins.</summary>
public class GymCandidate
{
    public const int NameMaxLength = 100;
    public const int NotesMaxLength = 1000;

    public Guid Id { get; private set; }
    public Guid SubmittedByUserId { get; private set; }
    public string GymName { get; private set; } = string.Empty;
    public string? OfficialEmail { get; private set; }
    public string? Website { get; private set; }
    public string City { get; private set; } = string.Empty;
    public string? Notes { get; private set; }
    public GymCandidateStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? HandledAt { get; private set; }
    public Guid? HandledByUserId { get; private set; }
    /// <summary>The gym created from this candidate, once accepted.</summary>
    public Guid? GymId { get; private set; }

    private GymCandidate() { }

    public static GymCandidate Submit(Guid userId, string gymName, string city, string? officialEmail, string? website, string? notes, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(),
        SubmittedByUserId = userId,
        GymName = gymName.Trim(),
        City = city.Trim(),
        OfficialEmail = string.IsNullOrWhiteSpace(officialEmail) ? null : officialEmail.Trim().ToLowerInvariant(),
        Website = string.IsNullOrWhiteSpace(website) ? null : website.Trim(),
        Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
        Status = GymCandidateStatus.Pending,
        CreatedAt = now,
    };

    public void SetStatus(GymCandidateStatus status, Guid adminUserId, DateTimeOffset now)
    {
        Status = status;
        HandledAt = now;
        HandledByUserId = adminUserId;
    }

    public void LinkGym(Guid gymId) => GymId = gymId;
}
