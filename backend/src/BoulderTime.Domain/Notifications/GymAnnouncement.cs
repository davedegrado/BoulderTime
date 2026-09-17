using BoulderTime.Domain.Common;

namespace BoulderTime.Domain.Notifications;

public enum AnnouncementType { Announcement = 0, Event = 1, ScheduleChange = 2, Maintenance = 3, Competition = 4, Other = 5 }

/// <summary>
/// A gym update published by staff. Whether followers are notified is decided at publication and never re-sent on edit.
/// An optional sector narrows the audience's relevance (sector followers are included even if they don't follow the gym).
/// </summary>
public class GymAnnouncement : IAuditable
{
    public const int TitleMaxLength = 120;
    public const int ContentMaxLength = 4000;

    public Guid Id { get; private set; }
    public Guid GymId { get; private set; }
    public Guid? SectorId { get; private set; }
    public Guid CreatedByUserId { get; private set; }
    public AnnouncementType Type { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Content { get; private set; } = string.Empty;
    public string? ImagePath { get; private set; }
    public DateTimeOffset? EventDate { get; private set; }
    public bool NotifyFollowers { get; private set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    private GymAnnouncement() { }

    public static GymAnnouncement Publish(Guid gymId, Guid userId, AnnouncementType type, string title, string content,
        Guid? sectorId, string? imagePath, DateTimeOffset? eventDate, bool notifyFollowers)
    {
        var a = new GymAnnouncement { Id = Guid.NewGuid(), GymId = gymId, CreatedByUserId = userId, NotifyFollowers = notifyFollowers };
        a.Edit(type, title, content, sectorId, imagePath, eventDate);
        return a;
    }

    public void Edit(AnnouncementType type, string title, string content, Guid? sectorId, string? imagePath, DateTimeOffset? eventDate)
    {
        Type = type;
        Title = title.Trim();
        Content = content.Trim();
        SectorId = sectorId;
        ImagePath = string.IsNullOrWhiteSpace(imagePath) ? null : imagePath.Trim();
        EventDate = eventDate;
    }
}
