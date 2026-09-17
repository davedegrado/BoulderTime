namespace BoulderTime.Domain.Notifications;

public enum NotificationType
{
    GymAnnouncement = 0,
    SectorRetraced = 1,
    BoulderUpdated = 2,
    OfficialBeta = 3,
    BoulderComments = 4,
    VideoApproved = 5,
    VideoRejected = 6,
    ReportReviewed = 7,
}

/// <summary>User-facing preference buckets. Every notification type belongs to exactly one.</summary>
public enum NotificationCategory { GymUpdates = 0, SectorUpdates = 1, BoulderUpdates = 2, MyContent = 3 }

public enum RelatedEntityType { Gym = 0, Sector = 1, Boulder = 2, Announcement = 3, Video = 4, Report = 5 }

/// <summary>
/// An in-app notification. <see cref="CollapseKey"/> lets bursts (e.g. several comments) fold into one unread item
/// instead of many. <see cref="Link"/> is an app-relative path the client opens when tapped.
/// </summary>
public class Notification
{
    public const int TitleMaxLength = 140;
    public const int BodyMaxLength = 500;

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public NotificationType Type { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string? Body { get; private set; }
    public RelatedEntityType RelatedEntityType { get; private set; }
    public Guid RelatedEntityId { get; private set; }
    public Guid? GymId { get; private set; }
    public string Link { get; private set; } = "/";
    public string? CollapseKey { get; private set; }
    /// <summary>How many events this notification represents (1 unless collapsed).</summary>
    public int Count { get; private set; } = 1;
    public DateTimeOffset? ReadAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Notification() { }

    public static Notification Create(Guid userId, NotificationType type, string title, string? body,
        RelatedEntityType relatedType, Guid relatedId, Guid? gymId, string link, string? collapseKey, DateTimeOffset now) => new()
    {
        Id = Guid.NewGuid(), UserId = userId, Type = type,
        Title = Truncate(title, TitleMaxLength)!, Body = Truncate(body, BodyMaxLength),
        RelatedEntityType = relatedType, RelatedEntityId = relatedId, GymId = gymId, Link = link,
        CollapseKey = collapseKey, CreatedAt = now,
    };

    /// <summary>Folds another event into this unread notification and bumps it to the top.</summary>
    public void Collapse(string title, string? body, DateTimeOffset now)
    {
        Count++;
        Title = Truncate(title, TitleMaxLength)!;
        Body = Truncate(body, BodyMaxLength);
        CreatedAt = now;
    }

    public void MarkRead(DateTimeOffset now) => ReadAt ??= now;

    public static NotificationCategory CategoryOf(NotificationType type) => type switch
    {
        NotificationType.GymAnnouncement => NotificationCategory.GymUpdates,
        NotificationType.SectorRetraced => NotificationCategory.SectorUpdates,
        NotificationType.BoulderUpdated or NotificationType.OfficialBeta or NotificationType.BoulderComments => NotificationCategory.BoulderUpdates,
        _ => NotificationCategory.MyContent,
    };

    private static string? Truncate(string? v, int max) => v is null ? null : v.Length <= max ? v : v[..(max - 1)] + "…";
}

/// <summary>Global per-user switches. A missing row means everything is on.</summary>
public class NotificationSettings
{
    public Guid UserId { get; private set; }
    public bool GymUpdates { get; private set; } = true;
    public bool SectorUpdates { get; private set; } = true;
    public bool BoulderUpdates { get; private set; } = true;
    public bool MyContent { get; private set; } = true;

    private NotificationSettings() { }

    public static NotificationSettings For(Guid userId) => new() { UserId = userId };

    public void Update(bool? gym, bool? sector, bool? boulder, bool? mine)
    {
        if (gym is { } g) GymUpdates = g;
        if (sector is { } s) SectorUpdates = s;
        if (boulder is { } b) BoulderUpdates = b;
        if (mine is { } m) MyContent = m;
    }

    public bool Allows(NotificationCategory c) => c switch
    {
        NotificationCategory.GymUpdates => GymUpdates,
        NotificationCategory.SectorUpdates => SectorUpdates,
        NotificationCategory.BoulderUpdates => BoulderUpdates,
        _ => MyContent,
    };
}
