using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Common;
using BoulderTime.Application.Gyms;
using BoulderTime.Domain.Boulders;
using BoulderTime.Domain.Staff;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Boulders;

/// <summary>
/// Boulder lifecycle. Viewing active boulders is public for visible gyms; a removed boulder stays readable by id
/// forever (climbing history links to it). Creating, editing, removing and restoring need STAFF+.
/// </summary>
public sealed class BoulderService(IAppDbContext db, GymAccess access, BoulderAccess boulderAccess, IObjectStorage storage, ICurrentUser currentUser, IClock clock, BoulderReader reader, Notifications.NotificationPublisher notifications)
{
    public const int MaxBulkRemove = 200;
    public const long MaxPhotoBytes = 10 * 1024 * 1024;
    public const long MaxThumbnailBytes = 512 * 1024;
    private static readonly Dictionary<string, string> PhotoTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = "jpg", ["image/png"] = "png", ["image/webp"] = "webp",
    };

    public static string PhotoPrefix(Guid gymId) => $"gyms/{gymId}/boulders/";

    // ---------- Read ----------

    public async Task<PagedResult<BoulderSummaryDto>> ListAsync(Guid gymId, BoulderQuery q, CancellationToken ct = default)
    {
        var gym = await db.Gyms.AsNoTracking().FirstOrDefaultAsync(g => g.Id == gymId, ct) ?? throw new NotFoundException("Gym", gymId);
        var role = await access.GetRoleAsync(gymId, ct);
        if (!gym.IsPubliclyVisible && role is null) throw new NotFoundException("Gym", gymId);

        // Climbers see removed boulders too: the ones they have already climbed are part of their own history, and
        // hiding them would mean a send stops existing the day the sector is retraced. Deleted ones are gone for
        // everybody, staff included — there is nothing left of them but the sends they carry.
        var status = q.Status ?? BoulderStatus.Active;
        if (status == BoulderStatus.Deleted) throw new NotFoundException("Gym", gymId);

        var (page, size) = Paging.Normalize(q.Page, q.PageSize, 24);
        var query = db.Boulders.AsNoTracking().Where(b => b.GymId == gymId && b.Status == status);
        if (q.SectorId is { } sectorId) query = query.Where(b => b.SectorId == sectorId);
        if (q.HoldColor is { } hold) query = query.Where(b => b.HoldColor == hold);
        if (q.GradeValueId is { } valueId) query = query.Where(b => db.BoulderGrades.Any(g => g.BoulderId == b.Id && g.GradeValueId == valueId));
        if (q.MinRating is { } min and >= 1 and <= 5)
            query = query.Where(b => db.BoulderRatings.Where(r => r.BoulderId == b.Id).Average(r => (double?)r.Rating) >= min);
        if (currentUser.UserId is { } uid && q.Progress is { } progress && progress != ProgressFilter.All)
        {
            query = progress switch
            {
                ProgressFilter.Completed => query.Where(b => db.BoulderAttempts.Any(a => a.BoulderId == b.Id && a.UserId == uid && a.Completed)),
                ProgressFilter.Projects => query.Where(b => db.BoulderAttempts.Any(a => a.BoulderId == b.Id && a.UserId == uid && !a.Completed && a.Attempts > 0)),
                _ => query.Where(b => !db.BoulderAttempts.Any(a => a.BoulderId == b.Id && a.UserId == uid)),
            };
        }

        var total = await query.CountAsync(ct);
        var boulders = await (status == BoulderStatus.Removed ? query.OrderByDescending(b => b.RemovedAt) : query.OrderByDescending(b => b.CreatedAt))
            .Skip((page - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<BoulderSummaryDto>(await reader.SummariesAsync(boulders, ct), page, size, total);
    }

    public async Task<BoulderDetailDto> GetAsync(Guid boulderId, CancellationToken ct = default)
    {
        // Asks the shared check rather than repeating it. Repeating it is what let a deleted boulder keep its page:
        // the rule grew a new clause and only the original copy was updated.
        var (b, gym, role) = await boulderAccess.RequireVisibleAsync(boulderId, ct);

        var sector = await db.Sectors.AsNoTracking().Where(s => s.Id == b.SectorId).Select(s => s.Name).FirstAsync(ct);
        var grades = (await reader.GradesAsync([b.Id], ct)).GetValueOrDefault(b.Id, []);
        var rating = (await reader.RatingsAsync([b.Id], ct)).GetValueOrDefault(b.Id, BoulderReader.NoRatings);
        var viewer = (await reader.ViewerAsync([b.Id], currentUser.UserId, ct)).GetValueOrDefault(b.Id);
        var following = currentUser.UserId is { } uid && await db.BoulderFollows.AnyAsync(f => f.BoulderId == b.Id && f.UserId == uid, ct);
        PersonDto? setter = null;
        if (b.SetterUserId is { } setterId)
            setter = await db.Users.AsNoTracking().Where(u => u.Id == setterId).Select(u => new PersonDto(u.Id, u.DisplayName, u.AvatarUrl)).FirstOrDefaultAsync(ct);

        return new BoulderDetailDto(b.Id, gym.Id, gym.Slug, gym.Name, b.SectorId, sector,
            storage.PublicUrl(StorageBuckets.BoulderImages, b.PhotoPath), b.PhotoPath, b.HoldColor, grades, setter,
            b.Status, b.CreatedAt, b.RemovedAt, role, rating, viewer, following, gym.CommunityVideosEnabled,
            await CanAddBetaAsync(gym, b.Id, ct),
            b.ThumbnailPath is null ? null : storage.PublicUrl(StorageBuckets.BoulderImages, b.ThumbnailPath));
    }

    /// <summary>
    /// Replacing an existing uploaded beta is always possible: it costs no extra storage. Only a brand-new upload
    /// counts against the gym's allowance — a linked beta stores nothing, so it neither counts nor is ever refused.
    /// This must agree with VideoService's own check, which is the one that actually refuses: this one only decides
    /// whether staff are offered the upload button, and offering a button that then fails is the worse answer.
    /// </summary>
    private async Task<bool> CanAddBetaAsync(Domain.Gyms.Gym gym, Guid boulderId, CancellationToken ct)
    {
        if (gym.OfficialBetaLimit is not { } limit) return true;
        var existing = await db.BoulderBetas.AsNoTracking().FirstOrDefaultAsync(x => x.BoulderId == boulderId, ct);
        if (existing is not null && !existing.IsLink) return true;
        var used = await db.BoulderBetas.AsNoTracking().Where(x => x.ExternalUrl == null)
            .Join(db.Boulders, x => x.BoulderId, b => b.Id, (x, b) => b.GymId)
            .CountAsync(id => id == gym.Id, ct);
        return used < limit;
    }

    // ---------- Photo upload ----------

    public async Task<UploadTicket> CreatePhotoUploadAsync(Guid gymId, PhotoUploadRequest r, CancellationToken ct = default)
    {
        await access.RequireRoleAsync(gymId, GymRole.Staff, ct);
        var thumbnail = r.Thumbnail == true;
        var max = thumbnail ? MaxThumbnailBytes : MaxPhotoBytes;
        new Validator()
            .Check(r.ContentType is not null && PhotoTypes.ContainsKey(r.ContentType), "contentType", "Upload a JPEG, PNG or WebP photo.")
            .Check(r.SizeBytes is > 0 && r.SizeBytes <= max, "sizeBytes", thumbnail ? "Thumbnails must be under 512 KB." : $"Photos must be under {MaxPhotoBytes / 1024 / 1024} MB.")
            .ThrowIfInvalid();
        var path = $"{PhotoPrefix(gymId)}{Guid.NewGuid():N}{(thumbnail ? ".thumb" : "")}.{PhotoTypes[r.ContentType!]}";
        return await storage.CreateUploadTicketAsync(StorageBuckets.BoulderImages, path, r.ContentType!.ToLowerInvariant(), max, ct) with { Resumable = null };
    }

    // ---------- Write ----------

    public async Task<BoulderDetailDto> CreateAsync(Guid gymId, SaveBoulderRequest r, CancellationToken ct = default)
    {
        var (gym, _) = await access.RequireRoleAsync(gymId, GymRole.Staff, ct);
        var valid = await ValidateAsync(gymId, r, currentPhotoPath: null, currentThumbnailPath: null, ct);
        var userId = currentUser.RequireUserId();

        var boulder = Boulder.Create(gymId, valid.SectorId, valid.PhotoPath, valid.HoldColor, valid.SetterUserId, userId, valid.ThumbnailPath);
        db.Boulders.Add(boulder);
        foreach (var (systemId, valueId) in valid.Grades)
            db.BoulderGrades.Add(BoulderGrade.Official(boulder.Id, systemId, valueId, userId, clock.UtcNow));

        // Notifications commit with the boulder. Summary: grade in the gym's primary system + hold colour, e.g. "6A · Blue holds".
        var sector = await db.Sectors.AsNoTracking().FirstAsync(x => x.Id == valid.SectorId, ct);
        var valueIds = valid.Grades.Select(g => g.ValueId).ToList();
        var primaryGrade = await db.GradeValues.AsNoTracking().Where(v => valueIds.Contains(v.Id))
            .Join(db.GradeSystems, v => v.GradeSystemId, s => s.Id, (v, s) => new { v.Label, s.SortOrder })
            .OrderBy(x => x.SortOrder).Select(x => x.Label).FirstOrDefaultAsync(ct);
        var summary = primaryGrade is null ? $"{valid.HoldColor} holds" : $"{primaryGrade} · {valid.HoldColor} holds";
        await notifications.BoulderCreatedAsync(boulder, gym, sector, summary, userId, ct);
        await db.SaveChangesAsync(ct);
        return await GetAsync(boulder.Id, ct);
    }

    /// <summary>
    /// The boulder's full photo, handed to its gym's staff through the API so the app can cut the card picture out of
    /// it on the device. Read from the public address instead, the browser refuses to let the app read the pixels
    /// whenever the storage's answer lacks the cross-origin header (or comes from a cache that dropped it).
    /// </summary>
    public async Task<(byte[] Content, string ContentType)> PhotoAsync(Guid boulderId, CancellationToken ct = default)
    {
        var boulder = await db.Boulders.AsNoTracking().FirstOrDefaultAsync(b => b.Id == boulderId, ct) ?? throw new NotFoundException("Boulder", boulderId);
        if (boulder.Status == BoulderStatus.Deleted) throw new NotFoundException("Boulder", boulderId);
        await access.RequireRoleAsync(boulder.GymId, GymRole.Staff, ct);
        var bytes = await storage.ReadAsync(StorageBuckets.BoulderImages, boulder.PhotoPath, ct) ?? throw new NotFoundException("Boulder photo", boulderId);
        var type = Path.GetExtension(boulder.PhotoPath).ToLowerInvariant() switch
        {
            ".png" => "image/png", ".webp" => "image/webp", _ => "image/jpeg",
        };
        return (bytes, type);
    }

    /// <summary>Corrects an existing boulder (wrong photo, grade or sector). Retracing is NOT an edit: remove and create instead.</summary>
    public async Task<BoulderDetailDto> UpdateAsync(Guid boulderId, SaveBoulderRequest r, CancellationToken ct = default)
    {
        var boulder = await db.Boulders.FirstOrDefaultAsync(b => b.Id == boulderId, ct) ?? throw new NotFoundException("Boulder", boulderId);
        if (boulder.Status == BoulderStatus.Deleted) throw new NotFoundException("Boulder", boulderId);
        await access.RequireRoleAsync(boulder.GymId, GymRole.Staff, ct);
        var valid = await ValidateAsync(boulder.GymId, r, boulder.PhotoPath, boulder.ThumbnailPath, ct);
        var userId = currentUser.RequireUserId();

        var changes = new List<Localization.NotificationTexts.BoulderChange>();
        if (boulder.SectorId != valid.SectorId) changes.Add(Localization.NotificationTexts.BoulderChange.Sector);
        if (boulder.HoldColor != valid.HoldColor) changes.Add(Localization.NotificationTexts.BoulderChange.HoldColor);
        if (boulder.PhotoPath != valid.PhotoPath) changes.Add(Localization.NotificationTexts.BoulderChange.Photo);
        var obsolete = boulder.Update(valid.SectorId, valid.PhotoPath, valid.ThumbnailPath, valid.HoldColor, valid.SetterUserId);
        var current = await db.BoulderGrades.Where(g => g.BoulderId == boulderId && g.Source == GradeSource.Staff).ToListAsync(ct);
        if (!current.Select(g => (g.GradeSystemId, g.GradeValueId)).ToHashSet().SetEquals(valid.Grades)) changes.Insert(0, Localization.NotificationTexts.BoulderChange.Grade);
        foreach (var g in current.Where(g => !valid.Grades.Contains((g.GradeSystemId, g.GradeValueId))))
            db.BoulderGrades.Remove(g);
        await db.SaveChangesAsync(ct); // free (boulder, system) slots before re-adding changed grades
        foreach (var (systemId, valueId) in valid.Grades.Where(v => !current.Any(g => g.GradeSystemId == v.SystemId && g.GradeValueId == v.ValueId)))
            db.BoulderGrades.Add(BoulderGrade.Official(boulderId, systemId, valueId, userId, clock.UtcNow));

        if (changes.Count > 0 && boulder.Status == BoulderStatus.Active)
        {
            var gym = await db.Gyms.AsNoTracking().FirstAsync(g => g.Id == boulder.GymId, ct);
            var sectorName = await db.Sectors.AsNoTracking().Where(x => x.Id == valid.SectorId).Select(x => x.Name).FirstAsync(ct);
            await notifications.BoulderUpdatedAsync(boulder, gym, sectorName, changes, userId, ct);
        }
        await db.SaveChangesAsync(ct);
        foreach (var old in obsolete) await storage.DeleteAsync(StorageBuckets.BoulderImages, old, ct);
        return await GetAsync(boulderId, ct);
    }

    /// <summary>
    /// Removes one or many boulders of a gym in one atomic step (sector retracing). Already-removed boulders are
    /// ignored. Every id must belong to the gym, otherwise nothing is removed.
    /// </summary>
    public async Task<RemoveBouldersResult> RemoveAsync(Guid gymId, RemoveBouldersRequest r, CancellationToken ct = default)
    {
        await access.RequireRoleAsync(gymId, GymRole.Staff, ct);
        var ids = (r.BoulderIds ?? []).Distinct().ToList();
        new Validator()
            .Check(ids.Count > 0, "boulderIds", "Select at least one boulder.")
            .Check(ids.Count <= MaxBulkRemove, "boulderIds", $"Remove at most {MaxBulkRemove} boulders at once.")
            .ThrowIfInvalid();

        var boulders = await db.Boulders.Where(b => ids.Contains(b.Id)).ToListAsync(ct);
        if (boulders.Count != ids.Count || boulders.Any(b => b.GymId != gymId))
            throw new ValidationException("boulderIds", "Some selected boulders don't belong to this gym.");

        var userId = currentUser.RequireUserId();
        var now = clock.UtcNow;
        var removed = boulders.Where(b => b.Remove(userId, now)).ToList();

        var sectorIds = removed.Select(b => b.SectorId).Distinct().ToList();
        var sectors = await db.Sectors.AsNoTracking().Where(s => sectorIds.Contains(s.Id)).ToDictionaryAsync(s => s.Id, ct);
        var groups = removed.GroupBy(b => b.SectorId).OrderBy(g => sectors.GetValueOrDefault(g.Key)?.Name).ToList();
        if (r.NotifyFollowers == true && groups.Count > 0)
        {
            // One "sector retraced" notification per sector per person — never one per boulder.
            var gym = await db.Gyms.AsNoTracking().FirstAsync(g => g.Id == gymId, ct);
            await notifications.SectorsRetracedAsync(gym,
                groups.Select(g => (sectors[g.Key], (IReadOnlyList<Guid>)g.Select(b => b.Id).ToList())).ToList(), userId, ct);
        }
        await ReleaseBetaVideosAsync(removed.Select(b => b.Id).ToList(), ct);
        await db.SaveChangesAsync(ct);

        var bySector = groups.Select(g => new SectorRemovalDto(g.Key, sectors.GetValueOrDefault(g.Key)?.Name ?? "", g.Count())).ToList();
        return new RemoveBouldersResult(removed.Count, bySector);
    }

    /// <summary>
    /// A removed boulder keeps its history but not its official beta: the video shows a route that is no longer on
    /// the wall, and it is the heaviest thing we store. Deleting it also frees a slot in the gym's beta allowance.
    /// Climbers' own videos are left alone — they belong to the people who filmed them, not to the gym.
    /// </summary>
    private async Task ReleaseBetaVideosAsync(List<Guid> boulderIds, CancellationToken ct)
    {
        if (boulderIds.Count == 0) return;
        var betas = await db.BoulderBetas.Where(b => boulderIds.Contains(b.BoulderId)).ToListAsync(ct);
        foreach (var beta in betas)
        {
            // Released() is empty for a linked beta: there is nothing of ours to delete, only the row.
            foreach (var path in beta.Released())
            {
                // A file that refuses to go must not stop the retrace: the wall has already changed.
                try { await storage.DeleteAsync(StorageBuckets.OfficialBeta, path, ct); }
                catch (Exception) { /* left for the storage clean-up to pick up */ }
            }
        }
        db.BoulderBetas.RemoveRange(betas);
    }

    /// <summary>
    /// What deleting this boulder would cost, so staff decide knowing it rather than guessing. Counted before the
    /// deed, because afterwards there is nothing left to count.
    /// </summary>
    public async Task<DeletionImpactDto> DeletionImpactAsync(Guid boulderId, CancellationToken ct = default)
    {
        var boulder = await db.Boulders.AsNoTracking().FirstOrDefaultAsync(b => b.Id == boulderId, ct) ?? throw new NotFoundException("Boulder", boulderId);
        if (boulder.Status == BoulderStatus.Deleted) throw new NotFoundException("Boulder", boulderId);
        await access.RequireRoleAsync(boulder.GymId, GymRole.Staff, ct);
        return new DeletionImpactDto(
            await db.BoulderAttempts.CountAsync(a => a.BoulderId == boulderId && a.Completed, ct),
            await db.Comments.CountAsync(c => c.BoulderId == boulderId, ct),
            await db.BoulderVideos.CountAsync(v => v.BoulderId == boulderId, ct),
            await db.BoulderBetas.AnyAsync(b => b.BoulderId == boulderId, ct));
    }

    /// <summary>
    /// Erases a removed boulder from the gym: photo, beta, climbers' videos, comments, ratings, grade suggestions
    /// and follows all go, and the storage behind them is freed.
    ///
    /// What does NOT go is the sends. A climber's history and their points are theirs, not the gym's, and a gym
    /// tidying its wall must not quietly take points off people who did the work. So the row stays as a marker,
    /// with its official grades — the leaderboard scores a send from the grade, so losing those would zero it —
    /// and every list and page stops showing it. It cannot be undone: there is nothing left to restore.
    /// </summary>
    public async Task DeleteAsync(Guid boulderId, CancellationToken ct = default)
    {
        var boulder = await db.Boulders.FirstOrDefaultAsync(b => b.Id == boulderId, ct) ?? throw new NotFoundException("Boulder", boulderId);
        await access.RequireRoleAsync(boulder.GymId, GymRole.Admin, ct);
        if (boulder.Status == BoulderStatus.Deleted) return;
        if (boulder.Status != BoulderStatus.Removed)
            throw new ConflictException("Take the boulder off the wall first, then delete it.", "boulder_not_removed");

        await ReleaseBetaVideosAsync([boulderId], ct);
        var videoIds = await ReleaseClimberVideosAsync(boulderId, ct);
        var comments = await db.Comments.Where(c => c.BoulderId == boulderId).ToListAsync(ct);
        db.Comments.RemoveRange(comments);   // their likes cascade
        db.BoulderRatings.RemoveRange(await db.BoulderRatings.Where(r => r.BoulderId == boulderId).ToListAsync(ct));
        db.GradeSuggestions.RemoveRange(await db.GradeSuggestions.Where(s => s.BoulderId == boulderId).ToListAsync(ct));
        db.BoulderFollows.RemoveRange(await db.BoulderFollows.Where(f => f.BoulderId == boulderId).ToListAsync(ct));

        // Reports point at content by id with no foreign key, so nothing would stop them outliving what they are
        // about and leaving the moderation queue full of rows that open onto nothing.
        var reported = comments.Select(c => c.Id).Concat(videoIds).Append(boulderId).ToList();
        db.Reports.RemoveRange(await db.Reports.Where(r => reported.Contains(r.EntityId)).ToListAsync(ct));

        foreach (var path in boulder.Delete(currentUser.UserId, clock.UtcNow))
        {
            try { await storage.DeleteAsync(StorageBuckets.BoulderImages, path, ct); }
            catch (Exception) { /* the row is going either way; the file is left for the storage clean-up */ }
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Climbers' own videos survive a removal, because they belong to the people who filmed them. They do not
    /// survive a deletion: the boulder they show no longer exists anywhere, so there is nothing left to watch.
    /// </summary>
    /// <returns>The ids of the videos that went, so their reports can go with them.</returns>
    private async Task<List<Guid>> ReleaseClimberVideosAsync(Guid boulderId, CancellationToken ct)
    {
        var videos = await db.BoulderVideos.Where(v => v.BoulderId == boulderId).ToListAsync(ct);
        foreach (var video in videos)
        {
            foreach (var path in new[] { video.StoragePath, video.ThumbnailPath })
            {
                if (string.IsNullOrWhiteSpace(path)) continue;
                try { await storage.DeleteAsync(StorageBuckets.CommunityVideos, path, ct); }
                catch (Exception) { /* left for the storage clean-up to pick up */ }
            }
        }
        db.BoulderVideos.RemoveRange(videos);
        return videos.Select(v => v.Id).ToList();
    }

    public async Task<BoulderDetailDto> RestoreAsync(Guid boulderId, CancellationToken ct = default)
    {
        var boulder = await db.Boulders.FirstOrDefaultAsync(b => b.Id == boulderId, ct) ?? throw new NotFoundException("Boulder", boulderId);
        // Loaded by id rather than through the usual visibility check, so the one thing that check does has to be
        // done here: a deleted boulder does not exist for anybody, and must not be restored into an empty shell.
        if (boulder.Status == BoulderStatus.Deleted) throw new NotFoundException("Boulder", boulderId);
        await access.RequireRoleAsync(boulder.GymId, GymRole.Staff, ct);
        var sectorActive = await db.Sectors.AnyAsync(s => s.Id == boulder.SectorId && s.IsActive, ct);
        if (!sectorActive) throw new ConflictException("Its sector is hidden. Show the sector again before restoring this boulder.", "sector_inactive");
        boulder.Restore();
        await db.SaveChangesAsync(ct);
        return await GetAsync(boulderId, ct);
    }

    // ---------- Helpers ----------

    private sealed record ValidBoulder(Guid SectorId, string PhotoPath, string? ThumbnailPath, HoldColor HoldColor, Guid? SetterUserId, HashSet<(Guid SystemId, Guid ValueId)> Grades);

    private async Task<ValidBoulder> ValidateAsync(Guid gymId, SaveBoulderRequest r, string? currentPhotoPath, string? currentThumbnailPath, CancellationToken ct)
    {
        var v = new Validator();
        var photo = Input.Trimmed(r.PhotoPath);
        v.Check(r.SectorId is not null, "sectorId", "Choose a sector.")
         .Check(photo.Length > 0, "photoPath", "Add a photo of the boulder.")
         .Check(r.HoldColor is not null && Enum.IsDefined(r.HoldColor.Value), "holdColor", "Choose the hold colour.")
         .ThrowIfInvalid();

        if (!await db.Sectors.AnyAsync(s => s.Id == r.SectorId && s.GymId == gymId && s.IsActive, ct))
            v.Check(false, "sectorId", "Choose an active sector of this gym.");

        if (photo != currentPhotoPath)
        {
            var wellFormed = photo.StartsWith(PhotoPrefix(gymId), StringComparison.Ordinal) && !photo.Contains("..") && !photo.Contains('\\') && !photo.Contains(".thumb.");
            if (!wellFormed || !await storage.ExistsAsync(StorageBuckets.BoulderImages, photo, ct))
                v.Check(false, "photoPath", "The photo upload didn't complete. Upload it again.");
        }

        // The small picture for the cards: made on the device, from the whole photo or from the part staff chose.
        // It can change on its own, without a new photo.
        string? thumbnail = null;
        var thumb = Input.Trimmed(r.ThumbnailPath);
        if (thumb.Length > 0 && thumb != currentThumbnailPath)
        {
            var thumbOk = thumb.StartsWith(PhotoPrefix(gymId), StringComparison.Ordinal) && thumb.Contains(".thumb.") && !thumb.Contains("..")
                          && await storage.ExistsAsync(StorageBuckets.BoulderImages, thumb, ct);
            if (thumbOk) thumbnail = thumb;
            else v.Check(false, "thumbnailPath", "The photo upload didn't complete. Upload it again.");
        }

        // A boulder can go on the wall before it has a grade: setters often decide the colour later. Climbers see it
        // as ungraded and can still suggest one; the leaderboard counts the send and adds the points once it's graded.
        var choices = r.Grades ?? [];
        var systemIds = choices.Select(c => c.GradeSystemId).ToList();
        if (choices.Any(c => c.GradeSystemId is null || c.GradeValueId is null) || systemIds.Distinct().Count() != systemIds.Count)
            v.Check(false, "grades", "Pick one grade per grading system.");
        else if (choices.Count > 0)
        {
            var valueIds = choices.Select(c => c.GradeValueId!.Value).ToList();
            var matches = await db.GradeValues.AsNoTracking()
                .Where(gv => valueIds.Contains(gv.Id) && gv.IsActive)
                .Join(db.GradeSystems, gv => gv.GradeSystemId, gs => gs.Id, (gv, gs) => new { gv.Id, SystemId = gs.Id, gs.GymId, gs.IsActive })
                .ToListAsync(ct);
            var ok = choices.All(c => matches.Any(m => m.Id == c.GradeValueId && m.SystemId == c.GradeSystemId && m.GymId == gymId && m.IsActive));
            v.Check(ok, "grades", "Use active grades from this gym's grading systems.");
        }

        if (r.SetterUserId is { } setterId && !await db.GymStaff.AnyAsync(s => s.GymId == gymId && s.UserId == setterId, ct))
            v.Check(false, "setterUserId", "The setter must be on this gym's staff.");

        v.ThrowIfInvalid();
        // When the photo is unchanged, the domain keeps the existing thumbnail.
        return new ValidBoulder(r.SectorId!.Value, photo, thumbnail, r.HoldColor!.Value, r.SetterUserId,
            choices.Select(c => (c.GradeSystemId!.Value, c.GradeValueId!.Value)).ToHashSet());
    }

}
