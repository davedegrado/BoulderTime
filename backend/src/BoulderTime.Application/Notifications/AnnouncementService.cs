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
    DateTimeOffset? EventDate, Guid? SectorId, string? SectorName, bool NotifiedFollowers, PersonDto Author, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt,
    /// <summary>Whether people can say they'll come: events only, not competitions or news.</summary>
    bool TakesAttendance = false,
    /// <summary>How many people said they'll come (suspended accounts and accounts on their way out don't count).</summary>
    int Attendees = 0,
    /// <summary>Whether the signed-in viewer said they'll come.</summary>
    bool ViewerAttending = false,
    /// <summary>Whether the viewer may see who is coming: the gym's followers and its staff.</summary>
    bool CanSeeAttendees = false);

public sealed record AttendanceDto(int Attendees, bool ViewerAttending);

public sealed record SaveAnnouncementRequest(
    AnnouncementType? Type, string? Title, string? Content, Guid? SectorId, string? ImagePath, DateTimeOffset? EventDate, bool? NotifyFollowers);

public sealed class AnnouncementService(IAppDbContext db, GymAccess access, IObjectStorage storage, ICurrentUser currentUser, NotificationPublisher publisher, IClock clock,
    Users.BlockedPeople blocked)
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
            // The ones the person took off their Home stay off it (the gym's page still has them).
            .Where(a => !db.AnnouncementDismissals.Any(d => d.UserId == userId && d.AnnouncementId == a.Id))
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

    // ---------- Taken off Home ----------

    /// <summary>Takes an update off the viewer's Home. Doing it twice is still once.</summary>
    public async Task HideFromHomeAsync(Guid announcementId, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var a = await VisibleAnnouncementAsync(announcementId, ct);
        if (await db.AnnouncementDismissals.AnyAsync(x => x.AnnouncementId == a.Id && x.UserId == userId, ct)) return;
        db.AnnouncementDismissals.Add(AnnouncementDismissal.Create(a.Id, userId, clock.UtcNow));
        try { await db.SaveChangesAsync(ct); }
        catch (UniqueConstraintViolationException) { /* hidden twice at once: hidden, which is the point */ }
    }

    /// <summary>Puts it back on Home.</summary>
    public async Task ShowOnHomeAsync(Guid announcementId, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var row = await db.AnnouncementDismissals.FirstOrDefaultAsync(x => x.AnnouncementId == announcementId && x.UserId == userId, ct);
        if (row is null) return;
        db.AnnouncementDismissals.Remove(row);
        await db.SaveChangesAsync(ct);
    }

    // ---------- "I'll be there" ----------

    /// <summary>
    /// How long after its date an event still takes names: the date is often the day rather than the hour, so the
    /// whole of that day counts.
    /// </summary>
    public static readonly TimeSpan AttendanceGrace = TimeSpan.FromDays(1);

    /// <summary>Says the viewer will come. Saying it again changes nothing.</summary>
    public async Task<AttendanceDto> AttendAsync(Guid announcementId, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var a = await VisibleAnnouncementAsync(announcementId, ct);
        if (!AnnouncementAttendance.TakesAttendance(a.Type))
            throw new ConflictException("Only events take names.", "attendance_not_offered");
        if (a.EventDate is { } date && date + AttendanceGrace < clock.UtcNow)
            throw new ConflictException("This event is over.", "event_over");
        if (!await db.AnnouncementAttendances.AnyAsync(x => x.AnnouncementId == a.Id && x.UserId == userId, ct))
        {
            db.AnnouncementAttendances.Add(AnnouncementAttendance.Create(a.Id, userId, clock.UtcNow));
            try { await db.SaveChangesAsync(ct); }
            catch (UniqueConstraintViolationException) { /* tapped twice at once: already there, which is the point */ }
        }
        return await AttendanceAsync(a.Id, userId, ct);
    }

    /// <summary>Takes the viewer's name off. Always possible, even after the event, and fine if it was never there.</summary>
    public async Task<AttendanceDto> WithdrawAttendanceAsync(Guid announcementId, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var a = await VisibleAnnouncementAsync(announcementId, ct);
        var row = await db.AnnouncementAttendances.FirstOrDefaultAsync(x => x.AnnouncementId == a.Id && x.UserId == userId, ct);
        if (row is not null)
        {
            db.AnnouncementAttendances.Remove(row);
            await db.SaveChangesAsync(ct);
        }
        return await AttendanceAsync(a.Id, userId, ct);
    }

    /// <summary>
    /// Who said they'll come, for the gym's followers and staff. People hidden from the viewer — blocked either way,
    /// suspended, leaving — are left out, as they are everywhere else.
    /// </summary>
    public async Task<IReadOnlyList<PersonDto>> AttendeesAsync(Guid announcementId, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var a = await VisibleAnnouncementAsync(announcementId, ct);
        if (!await CanSeeAttendeesAsync(a.GymId, userId, ct))
            throw new ForbiddenException("Follow the gym to see who is coming.", "follow_required");
        var hidden = await blocked.ForViewerAsync(ct);
        var rows = await db.AnnouncementAttendances.AsNoTracking()
            .Where(x => x.AnnouncementId == a.Id && !hidden.Contains(x.UserId))
            .Join(db.Users, x => x.UserId, u => u.Id, (x, u) => new { x.CreatedAt, u.Id, u.DisplayName, u.AvatarUrl })
            .ToListAsync(ct);
        return rows.OrderBy(x => x.CreatedAt).Select(x => new PersonDto(x.Id, x.DisplayName, x.AvatarUrl)).ToList();
    }

    private async Task<GymAnnouncement> VisibleAnnouncementAsync(Guid announcementId, CancellationToken ct)
    {
        var a = await db.GymAnnouncements.AsNoTracking().FirstOrDefaultAsync(x => x.Id == announcementId, ct)
                ?? throw new NotFoundException("Announcement", announcementId);
        var gym = await db.Gyms.AsNoTracking().FirstAsync(g => g.Id == a.GymId, ct);
        if (!gym.IsPubliclyVisible && await access.GetRoleAsync(gym.Id, ct) is null) throw new NotFoundException("Announcement", announcementId);
        return a;
    }

    private async Task<bool> CanSeeAttendeesAsync(Guid gymId, Guid userId, CancellationToken ct) =>
        await db.GymFollows.AnyAsync(f => f.GymId == gymId && f.UserId == userId, ct) || await access.GetRoleAsync(gymId, ct) is not null;

    private async Task<AttendanceDto> AttendanceAsync(Guid announcementId, Guid userId, CancellationToken ct)
    {
        var counts = await CountsAsync([announcementId], ct);
        var mine = await db.AnnouncementAttendances.AnyAsync(x => x.AnnouncementId == announcementId && x.UserId == userId, ct);
        return new AttendanceDto(counts.GetValueOrDefault(announcementId), mine);
    }

    /// <summary>People coming to each event. Accounts hidden from everybody (suspended, leaving) are not counted.</summary>
    private async Task<Dictionary<Guid, int>> CountsAsync(List<Guid> announcementIds, CancellationToken ct)
    {
        if (announcementIds.Count == 0) return [];
        return await db.AnnouncementAttendances.AsNoTracking()
            .Where(x => announcementIds.Contains(x.AnnouncementId))
            .Join(db.Users.Where(u => u.SuspendedAt == null && u.DeletionRequestedAt == null), x => x.UserId, u => u.Id, (x, u) => x.AnnouncementId)
            .GroupBy(id => id).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
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

        var events = rows.Where(a => AnnouncementAttendance.TakesAttendance(a.Type)).Select(a => a.Id).ToList();
        var counts = await CountsAsync(events, ct);
        var mine = new HashSet<Guid>();
        var canSee = new HashSet<Guid>();
        if (currentUser.UserId is { } uid && events.Count > 0)
        {
            mine = (await db.AnnouncementAttendances.AsNoTracking().Where(x => x.UserId == uid && events.Contains(x.AnnouncementId))
                .Select(x => x.AnnouncementId).ToListAsync(ct)).ToHashSet();
            foreach (var gymId in rows.Where(a => events.Contains(a.Id)).Select(a => a.GymId).Distinct())
                if (await CanSeeAttendeesAsync(gymId, uid, ct)) canSee.Add(gymId);
        }

        return rows.Select(a => new AnnouncementDto(a.Id, a.GymId, gyms[a.GymId].Name, gyms[a.GymId].Slug, a.Type, a.Title, a.Content,
            a.ImagePath is null ? null : storage.PublicUrl(StorageBuckets.GymImages, a.ImagePath), a.ImagePath, a.EventDate,
            a.SectorId, a.SectorId is { } sid ? sectors.GetValueOrDefault(sid) : null, a.NotifyFollowers,
            authors.GetValueOrDefault(a.CreatedByUserId, new PersonDto(a.CreatedByUserId, "Staff", null)), a.CreatedAt, a.UpdatedAt,
            AnnouncementAttendance.TakesAttendance(a.Type), counts.GetValueOrDefault(a.Id), mine.Contains(a.Id),
            AnnouncementAttendance.TakesAttendance(a.Type) && canSee.Contains(a.GymId))).ToList();
    }
}
