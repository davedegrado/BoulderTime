namespace BoulderTime.Domain.Community;

public enum ReportEntityType { Comment = 0, Video = 1, Boulder = 2 }
public enum ReportReason { Inappropriate = 0, WrongBoulder = 1, Spam = 2, Misleading = 3, Other = 4 }
public enum ReportStatus { Pending = 0, Resolved = 1, Dismissed = 2 }

/// <summary>
/// A user's report about content. <see cref="GymId"/> is stored at creation so gym staff can moderate their own
/// gym's reports and platform admins can moderate everything.
/// </summary>
public class Report
{
    public const int DescriptionMaxLength = 500;

    public Guid Id { get; private set; }
    public Guid ReportedByUserId { get; private set; }
    public ReportEntityType EntityType { get; private set; }
    public Guid EntityId { get; private set; }
    public Guid GymId { get; private set; }
    public ReportReason Reason { get; private set; }
    public string? Description { get; private set; }
    public ReportStatus Status { get; private set; } = ReportStatus.Pending;
    public string? ResolutionNote { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public DateTimeOffset? ReviewedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Report() { }

    public static Report Create(Guid reporterId, ReportEntityType type, Guid entityId, Guid gymId, ReportReason reason, string? description, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), ReportedByUserId = reporterId, EntityType = type, EntityId = entityId, GymId = gymId,
        Reason = reason, Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(), CreatedAt = now,
    };

    public void Close(ReportStatus status, Guid reviewerId, string? note, DateTimeOffset now)
    {
        if (status == ReportStatus.Pending) throw new ArgumentException("Close a report as resolved or dismissed.", nameof(status));
        if (Status != ReportStatus.Pending) throw new InvalidOperationException("Report already closed.");
        Status = status;
        ReviewedByUserId = reviewerId;
        ReviewedAt = now;
        ResolutionNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}
