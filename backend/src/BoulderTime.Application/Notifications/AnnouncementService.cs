using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Boulders;
using BoulderTime.Application.Common;
using BoulderTime.Application.Gyms;
using BoulderTime.Domain.Notifications;
using BoulderTime.Domain.Staff;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Notifications;

public sealed record AnnouncementDto(
    Guid Id, Guid GymId, string GymName, string GymSlug, AnnouncementType Type, string Title, string Content, string? ImageUrl, string? ImagePath,
    DateTimeOffset? EventDate, Guid? SectorId, string? SectorName, bool NotifiedFollowers, PersonDto Author, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record SaveAnnouncementRequest(
    AnnouncementType? Type, string? Title, string? Content, Guid? SectorId, string? ImagePath, DateTimeOffset? EventDate, bool? NotifyFollowers);

public sealed class AnnouncementService(IAppDbContext db, GymAccess access, IObjectStorage storage, ICurrentUser currentUser, NotificationPublisher publisher)
{
    private static readonly Dictionary<string, string> ImageTypes = new(StringComparer.OrdinalIgnoreCase) { ["image/jpeg"] = "jpg", ["image/png"] = "png", ["image/webp"] = "webp" };
    public static string ImagePrefix(Guid gymId) => $"gyms/{gymId}/announcements/";

    public async Task<PagedResult<AnnouncementDto>> ListAsync(Guid gymId, int? page, int? pageSize, CancellationToken ct = default)
    {
        var gym = await db.Gyms.AsNoTracking().FirstOrDefaultAsync(g => g.Id == gymId, ct) ?? throw new NotFoundException("Gym", gymId);
        if (!gym.IsPubliclyVisible && await access.GetRoleAsync(gymId, ct) is null) throw new NotFoundException("Gym", gymId);
        var (p, size) = Paging.Normalize(page, pageSize, 20);
        var q = db.GymAnnouncements.AsNoTracking().Where(a => a.GymId == gymId);
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(a => a.CreatedAt).Skip((p - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<AnnouncementDto>(await ToDtosAsync(rows, ct), p, size, total);
    }

    /// <summary>Latest announcements from gyms the user follows, for Home.</summary>
    public async Task<IReadOnlyList<AnnouncementDto>> ForFollowedGymsAsync(Guid userId, int take, CancellationToken ct)
    {
        var rows = await db.GymAnnouncements.AsNoTracking()
            .Where(a => db.GymFollows.Any(f => f.UserId == userId && f.GymId == a.GymId))
            .Join(db.Gyms, a => a.GymId, g => g.Id, (a, g) => new { a, g.Status })
            .Where(x => x.Status == Domain.Gyms.GymStatus.Active)
            .OrderByDescending(x => x.a.CreatedAt).Take(take).Select(x => x.a).ToListAsync(ct);
        return await ToDtosAsync(rows, ct);
    }

    public async Task<UploadTicket> CreateImageUploadAsync(Guid gymId, Boulders.PhotoUploadRequest r, CancellationToken ct = default)
    {
        await access.RequireRoleAsync(gymId, GymRole.Staff, ct);
        new Validator()
            .Check(r.ContentType is not null && ImageTypes.ContainsKey(r.ContentType), "contentType", "Upload a JPEG, PNG or WebP image.")
            .Check(r.SizeBytes is > 0 and <= BoulderService.MaxPhotoBytes, "sizeBytes", "Images must be under 10 MB.")
            .ThrowIfInvalid();
        var path = $"{ImagePrefix(gymId)}{Guid.NewGuid():N}.{ImageTypes[r.ContentType!]}";
        return await storage.CreateUploadTicketAsync(StorageBuckets.GymImages, path, r.ContentType!.ToLowerInvariant(), BoulderService.MaxPhotoBytes, ct);
    }

    public async Task<AnnouncementDto> CreateAsync(Guid gymId, SaveAnnouncementRequest r, CancellationToken ct = default)
    {
        var (gym, _) = await access.RequireRoleAsync(gymId, GymRole.Staff, ct);
        var userId = currentUser.RequireUserId();
        var sectorName = await ValidateAsync(gymId, r, currentImage: null, ct);

        var a = GymAnnouncement.Publish(gymId, userId, r.Type!.Value, r.Title!, r.Content!, r.SectorId, r.ImagePath, r.EventDate, r.NotifyFollowers ?? false);
        db.GymAnnouncements.Add(a);
        await publisher.AnnouncementAsync(a, gym, sectorName, ct);
        await db.SaveChangesAsync(ct); // announcement and its notifications commit together
        return (await ToDtosAsync([a], ct))[0];
    }

    /// <summary>Edits never re-notify followers.</summary>
    public async Task<AnnouncementDto> UpdateAsync(Guid announcementId, SaveAnnouncementRequest r, CancellationToken ct = default)
    {
        var a = await db.GymAnnouncements.FirstOrDefaultAsync(x => x.Id == announcementId, ct) ?? throw new NotFoundException("Announcement", announcementId);
        await access.RequireRoleAsync(a.GymId, GymRole.Staff, ct);
        await ValidateAsync(a.GymId, r, a.ImagePath, ct);
        a.Edit(r.Type!.Value, r.Title!, r.Content!, r.SectorId, r.ImagePath, r.EventDate);
        await db.SaveChangesAsync(ct);
        return (await ToDtosAsync([a], ct))[0];
    }

    public async Task DeleteAsync(Guid announcementId, CancellationToken ct = default)
    {
        var a = await db.GymAnnouncements.FirstOrDefaultAsync(x => x.Id == announcementId, ct) ?? throw new NotFoundException("Announcement", announcementId);
        await access.RequireRoleAsync(a.GymId, GymRole.Staff, ct);
        db.GymAnnouncements.Remove(a);
        await db.SaveChangesAsync(ct);
        if (a.ImagePath is not null) await storage.DeleteAsync(StorageBuckets.GymImages, a.ImagePath, ct);
    }

    private async Task<string?> ValidateAsync(Guid gymId, SaveAnnouncementRequest r, string? currentImage, CancellationToken ct)
    {
        var v = new Validator()
            .Check(r.Type is not null && Enum.IsDefined(r.Type.Value), "type", "Choose a type.")
            .Check(Input.Trimmed(r.Title).Length >= 3, "title", "Write a title.")
            .Check(Input.MaxLength(r.Title, GymAnnouncement.TitleMaxLength), "title", $"Keep the title to {GymAnnouncement.TitleMaxLength} characters or fewer.")
            .Check(Input.Trimmed(r.Content).Length >= 1, "content", "Write the announcement.")
            .Check(Input.MaxLength(r.Content, GymAnnouncement.ContentMaxLength), "content", $"Keep it to {GymAnnouncement.ContentMaxLength} characters or fewer.")
            .Check(r.Type is not (AnnouncementType.Event or AnnouncementType.Competition) || r.EventDate is not null, "eventDate", "Events and competitions need a date.");
        v.ThrowIfInvalid();

        string? sectorName = null;
        if (r.SectorId is { } sectorId)
        {
            sectorName = await db.Sectors.AsNoTracking().Where(s => s.Id == sectorId && s.GymId == gymId).Select(s => s.Name).FirstOrDefaultAsync(ct)
                         ?? throw new ValidationException("sectorId", "Choose a sector of this gym.");
        }
        var image = Input.Trimmed(r.ImagePath);
        if (image.Length > 0 && image != currentImage)
        {
            var ok = image.StartsWith(ImagePrefix(gymId), StringComparison.Ordinal) && !image.Contains("..") && await storage.ExistsAsync(StorageBuckets.GymImages, image, ct);
            if (!ok) throw new ValidationException("imagePath", "The image upload didn't complete. Upload it again.");
        }
        return sectorName;
    }

    private async Task<IReadOnlyList<AnnouncementDto>> ToDtosAsync(IReadOnlyList<GymAnnouncement> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return [];
        var gymIds = rows.Select(a => a.GymId).Distinct().ToList();
        var gyms = await db.Gyms.AsNoTracking().Where(g => gymIds.Contains(g.Id)).ToDictionaryAsync(g => g.Id, ct);
        var sectorIds = rows.Where(a => a.SectorId != null).Select(a => a.SectorId!.Value).Distinct().ToList();
        var sectors = await db.Sectors.AsNoTracking().Where(s => sectorIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, s => s.Name, ct);
        var authorIds = rows.Select(a => a.CreatedByUserId).Distinct().ToList();
        var authors = await db.Users.AsNoTracking().Where(u => authorIds.Contains(u.Id)).ToDictionaryAsync(u => u.Id, u => new PersonDto(u.Id, u.DisplayName, u.AvatarUrl), ct);
        return rows.Select(a => new AnnouncementDto(a.Id, a.GymId, gyms[a.GymId].Name, gyms[a.GymId].Slug, a.Type, a.Title, a.Content,
            a.ImagePath is null ? null : storage.PublicUrl(StorageBuckets.GymImages, a.ImagePath), a.ImagePath, a.EventDate,
            a.SectorId, a.SectorId is { } sid ? sectors.GetValueOrDefault(sid) : null, a.NotifyFollowers,
            authors.GetValueOrDefault(a.CreatedByUserId, new PersonDto(a.CreatedByUserId, "Staff", null)), a.CreatedAt, a.UpdatedAt)).ToList();
    }
}
