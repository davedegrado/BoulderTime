namespace BoulderTime.Domain.Follows;

/// <summary>
/// Gym, sector and boulder follows are independent subscriptions. <c>NotificationsEnabled</c> is stored now and
/// used for targeting when notifications arrive (Phase 6). A user can have any number of favourite gyms.
/// </summary>
public class GymFollow
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid GymId { get; private set; }
    public bool IsFavorite { get; private set; }
    public bool NotificationsEnabled { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; }

    private GymFollow() { }

    public static GymFollow Create(Guid userId, Guid gymId, DateTimeOffset now) =>
        new() { Id = Guid.NewGuid(), UserId = userId, GymId = gymId, CreatedAt = now };

    public void Update(bool? isFavorite, bool? notificationsEnabled)
    {
        if (isFavorite is { } f) IsFavorite = f;
        if (notificationsEnabled is { } n) NotificationsEnabled = n;
    }
}

public class SectorFollow
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid SectorId { get; private set; }
    public bool NotificationsEnabled { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; }

    private SectorFollow() { }

    public static SectorFollow Create(Guid userId, Guid sectorId, DateTimeOffset now) =>
        new() { Id = Guid.NewGuid(), UserId = userId, SectorId = sectorId, CreatedAt = now };

    public void SetNotifications(bool? enabled) { if (enabled is { } e) NotificationsEnabled = e; }
}

public class BoulderFollow
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid BoulderId { get; private set; }
    public bool NotificationsEnabled { get; private set; } = true;
    public DateTimeOffset CreatedAt { get; private set; }

    private BoulderFollow() { }

    public static BoulderFollow Create(Guid userId, Guid boulderId, DateTimeOffset now) =>
        new() { Id = Guid.NewGuid(), UserId = userId, BoulderId = boulderId, CreatedAt = now };

    public void SetNotifications(bool? enabled) { if (enabled is { } e) NotificationsEnabled = e; }
}
