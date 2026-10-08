namespace BoulderTime.Domain.Users;

public enum UserReportReason { Harassment = 0, Spam = 1, Impersonation = 2, InappropriateProfile = 3, Other = 4 }

public enum UserReportStatus { Pending = 0, Suspended = 1, Dismissed = 2 }

/// <summary>
/// Someone telling BoulderTime that a person — not a single comment or video — is abusing the service. Content reports
/// go to the gym that owns the boulder (ADR-011); a person spans every gym, so only BoulderTime decides, and the only
/// outcomes are a platform-wide suspension or a dismissal. Apple's guideline 1.2 asks for exactly this.
/// </summary>
public class UserReport
{
    public const int DescriptionMaxLength = 500;

    public Guid Id { get; private set; }
    public Guid ReportedUserId { get; private set; }
    public Guid ReportedByUserId { get; private set; }
    public UserReportReason Reason { get; private set; }
    public string? Description { get; private set; }
    public UserReportStatus Status { get; private set; } = UserReportStatus.Pending;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? HandledAt { get; private set; }
    public Guid? HandledByUserId { get; private set; }
    public string? HandlingNote { get; private set; }

    private UserReport() { }

    public static UserReport Create(Guid reportedUserId, Guid reportedBy, UserReportReason reason, string? description, DateTimeOffset now)
    {
        if (reportedUserId == reportedBy) throw new ArgumentException("You can't report yourself.", nameof(reportedUserId));
        var text = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        return new UserReport
        {
            Id = Guid.NewGuid(), ReportedUserId = reportedUserId, ReportedByUserId = reportedBy, Reason = reason,
            Description = text is { Length: > DescriptionMaxLength } ? text[..DescriptionMaxLength] : text, CreatedAt = now,
        };
    }

    public void Handle(UserReportStatus outcome, Guid byUserId, string? note, DateTimeOffset now)
    {
        if (outcome == UserReportStatus.Pending) throw new ArgumentException("Pending is not an outcome.", nameof(outcome));
        Status = outcome;
        HandledByUserId = byUserId;
        HandledAt = now;
        var text = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        HandlingNote = text is { Length: > DescriptionMaxLength } ? text[..DescriptionMaxLength] : text;
    }
}
