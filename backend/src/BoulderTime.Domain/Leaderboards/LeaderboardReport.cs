namespace BoulderTime.Domain.Leaderboards;

public enum LeaderboardReportStatus { Pending = 0, Excluded = 1, Dismissed = 2 }

/// <summary>
/// A gym telling BoulderTime that someone's leaderboard results look implausible. Gyms report; only BoulderTime
/// decides, so an accusation never becomes a punishment inside the gym that made it.
/// </summary>
public class LeaderboardReport
{
    public const int ReasonMaxLength = 500;

    public Guid Id { get; private set; }
    public Guid GymId { get; private set; }
    public Guid ReportedUserId { get; private set; }
    public Guid ReportedByUserId { get; private set; }
    public string Reason { get; private set; } = "";
    public LeaderboardReportStatus Status { get; private set; } = LeaderboardReportStatus.Pending;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? HandledAt { get; private set; }
    public Guid? HandledByUserId { get; private set; }
    public string? HandlingNote { get; private set; }

    private LeaderboardReport() { }

    public static LeaderboardReport Create(Guid gymId, Guid reportedUserId, Guid reportedBy, string reason, DateTimeOffset now)
    {
        var text = (reason ?? "").Trim();
        if (text.Length == 0) throw new ArgumentException("A reason is required.", nameof(reason));
        if (reportedUserId == reportedBy) throw new ArgumentException("You can't report yourself.", nameof(reportedUserId));
        return new LeaderboardReport
        {
            Id = Guid.NewGuid(), GymId = gymId, ReportedUserId = reportedUserId, ReportedByUserId = reportedBy,
            Reason = text.Length > ReasonMaxLength ? text[..ReasonMaxLength] : text, CreatedAt = now,
        };
    }

    public void Handle(LeaderboardReportStatus outcome, Guid byUserId, string? note, DateTimeOffset now)
    {
        if (outcome == LeaderboardReportStatus.Pending) throw new ArgumentException("Pending is not an outcome.", nameof(outcome));
        Status = outcome;
        HandledByUserId = byUserId;
        HandledAt = now;
        HandlingNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
}
