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
public sealed class BoulderService(IAppDbContext db, GymAccess access, IObjectStorage storage, ICurrentUser currentUser, IClock clock, BoulderReader reader, Notifications.NotificationPublisher notifications)
{
    public const int MaxBulkRemove = 200;
    public const long MaxPhotoBytes = 10 * 1024 * 1024;
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

        var status = q.Status ?? BoulderStatus.Active;
        if (status == BoulderStatus.Removed && role is null)
            throw new ForbiddenException("Only staff can browse removed boulders.", "gym_staff_required");

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
        var b = await db.Boulders.AsNoTracking().FirstOrDefaultAsync(x => x.Id == boulderId, ct) ?? throw new NotFoundException("Boulder", boulderId);
        var gym = await db.Gyms.AsNoTracking().FirstAsync(g => g.Id == b.GymId, ct);
        var role = await access.GetRoleAsync(gym.Id, ct);
        if (!gym.IsPubliclyVisible && role is null) throw new NotFoundException("Boulder", boulderId);

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
            b.Status, b.CreatedAt, b.RemovedAt, role, rating, viewer, following);
    }

    // ---------- Photo upload ----------

    public async Task<UploadTicket> CreatePhotoUploadAsync(Guid gymId, PhotoUploadRequest r, CancellationToken ct = default)
    {
        await access.RequireRoleAsync(gymId, GymRole.Staff, ct);
        new Validator()
            .Check(r.ContentType is not null && PhotoTypes.ContainsKey(r.ContentType), "contentType", "Upload a JPEG, PNG or WebP photo.")
            .Check(r.SizeBytes is > 0 and <= MaxPhotoBytes, "sizeBytes", $"Photos must be under {MaxPhotoBytes / 1024 / 1024} MB.")
            .ThrowIfInvalid();
        var path = $"{PhotoPrefix(gymId)}{Guid.NewGuid():N}.{PhotoTypes[r.ContentType!]}";
        return await storage.CreateUploadTicketAsync(StorageBuckets.BoulderImages, path, r.ContentType!.ToLowerInvariant(), MaxPhotoBytes, ct);
    }

    // ---------- Write ----------

    public async Task<BoulderDetailDto> CreateAsync(Guid gymId, SaveBoulderRequest r, CancellationToken ct = default)
    {
        await access.RequireRoleAsync(gymId, GymRole.Staff, ct);
        var valid = await ValidateAsync(gymId, r, currentPhotoPath: null, ct);
        var userId = currentUser.RequireUserId();

        var boulder = Boulder.Create(gymId, valid.SectorId, valid.PhotoPath, valid.HoldColor, valid.SetterUserId, userId);
        db.Boulders.Add(boulder);
        foreach (var (systemId, valueId) in valid.Grades)
            db.BoulderGrades.Add(BoulderGrade.Official(boulder.Id, systemId, valueId, userId, clock.UtcNow));
        await db.SaveChangesAsync(ct);
        return await GetAsync(boulder.Id, ct);
    }

    /// <summary>Corrects an existing boulder (wrong photo, grade or sector). Retracing is NOT an edit: remove and create instead.</summary>
    public async Task<BoulderDetailDto> UpdateAsync(Guid boulderId, SaveBoulderRequest r, CancellationToken ct = default)
    {
        var boulder = await db.Boulders.FirstOrDefaultAsync(b => b.Id == boulderId, ct) ?? throw new NotFoundException("Boulder", boulderId);
        await access.RequireRoleAsync(boulder.GymId, GymRole.Staff, ct);
        var valid = await ValidateAsync(boulder.GymId, r, boulder.PhotoPath, ct);
        var userId = currentUser.RequireUserId();

        var changes = new List<string>();
        if (boulder.SectorId != valid.SectorId) changes.Add("moved to another sector");
        if (boulder.HoldColor != valid.HoldColor) changes.Add("hold colour corrected");
        if (boulder.PhotoPath != valid.PhotoPath) changes.Add("new photo");
        boulder.Update(valid.SectorId, valid.PhotoPath, valid.HoldColor, valid.SetterUserId);
        var current = await db.BoulderGrades.Where(g => g.BoulderId == boulderId && g.Source == GradeSource.Staff).ToListAsync(ct);
        if (!current.Select(g => (g.GradeSystemId, g.GradeValueId)).ToHashSet().SetEquals(valid.Grades)) changes.Insert(0, "grade changed");
        foreach (var g in current.Where(g => !valid.Grades.Contains((g.GradeSystemId, g.GradeValueId))))
            db.BoulderGrades.Remove(g);
        await db.SaveChangesAsync(ct); // free (boulder, system) slots before re-adding changed grades
        foreach (var (systemId, valueId) in valid.Grades.Where(v => !current.Any(g => g.GradeSystemId == v.SystemId && g.GradeValueId == v.ValueId)))
            db.BoulderGrades.Add(BoulderGrade.Official(boulderId, systemId, valueId, userId, clock.UtcNow));

        if (changes.Count > 0 && boulder.Status == BoulderStatus.Active)
        {
            var gym = await db.Gyms.AsNoTracking().FirstAsync(g => g.Id == boulder.GymId, ct);
            var sectorName = await db.Sectors.AsNoTracking().Where(x => x.Id == valid.SectorId).Select(x => x.Name).FirstAsync(ct);
            await notifications.BoulderUpdatedAsync(boulder, gym, sectorName, string.Join(", ", changes), userId, ct);
        }
        await db.SaveChangesAsync(ct);
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
        await db.SaveChangesAsync(ct);

        var bySector = groups.Select(g => new SectorRemovalDto(g.Key, sectors.GetValueOrDefault(g.Key)?.Name ?? "", g.Count())).ToList();
        return new RemoveBouldersResult(removed.Count, bySector);
    }

    public async Task<BoulderDetailDto> RestoreAsync(Guid boulderId, CancellationToken ct = default)
    {
        var boulder = await db.Boulders.FirstOrDefaultAsync(b => b.Id == boulderId, ct) ?? throw new NotFoundException("Boulder", boulderId);
        await access.RequireRoleAsync(boulder.GymId, GymRole.Staff, ct);
        var sectorActive = await db.Sectors.AnyAsync(s => s.Id == boulder.SectorId && s.IsActive, ct);
        if (!sectorActive) throw new ConflictException("Its sector is hidden. Show the sector again before restoring this boulder.", "sector_inactive");
        boulder.Restore();
        await db.SaveChangesAsync(ct);
        return await GetAsync(boulderId, ct);
    }

    // ---------- Helpers ----------

    private sealed record ValidBoulder(Guid SectorId, string PhotoPath, HoldColor HoldColor, Guid? SetterUserId, HashSet<(Guid SystemId, Guid ValueId)> Grades);

    private async Task<ValidBoulder> ValidateAsync(Guid gymId, SaveBoulderRequest r, string? currentPhotoPath, CancellationToken ct)
    {
        var v = new Validator();
        var photo = Input.Trimmed(r.PhotoPath);
        v.Check(r.SectorId is not null, "sectorId", "Choose a sector.")
         .Check(photo.Length > 0, "photoPath", "Add a photo of the boulder.")
         .Check(r.HoldColor is not null && Enum.IsDefined(r.HoldColor.Value), "holdColor", "Choose the hold colour.")
         .Check(r.Grades is { Count: > 0 }, "grades", "Give the boulder at least one official grade.")
         .ThrowIfInvalid();

        if (!await db.Sectors.AnyAsync(s => s.Id == r.SectorId && s.GymId == gymId && s.IsActive, ct))
            v.Check(false, "sectorId", "Choose an active sector of this gym.");

        if (photo != currentPhotoPath)
        {
            var wellFormed = photo.StartsWith(PhotoPrefix(gymId), StringComparison.Ordinal) && !photo.Contains("..") && !photo.Contains('\\');
            if (!wellFormed || !await storage.ExistsAsync(StorageBuckets.BoulderImages, photo, ct))
                v.Check(false, "photoPath", "The photo upload didn't complete. Upload it again.");
        }

        var choices = r.Grades!;
        var systemIds = choices.Select(c => c.GradeSystemId).ToList();
        if (choices.Any(c => c.GradeSystemId is null || c.GradeValueId is null) || systemIds.Distinct().Count() != systemIds.Count)
            v.Check(false, "grades", "Pick one grade per grading system.");
        else
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
        return new ValidBoulder(r.SectorId!.Value, photo, r.HoldColor!.Value, r.SetterUserId,
            choices.Select(c => (c.GradeSystemId!.Value, c.GradeValueId!.Value)).ToHashSet());
    }

}
