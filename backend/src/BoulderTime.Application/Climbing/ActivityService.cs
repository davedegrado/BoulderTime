using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Boulders;
using BoulderTime.Application.Common;
using BoulderTime.Application.Follows;
using BoulderTime.Application.Gyms;
using BoulderTime.Domain.Boulders;
using BoulderTime.Domain.Climbing;
using BoulderTime.Domain.Grading;
using BoulderTime.Domain.Gyms;
using BoulderTime.Domain.Staff;
using BoulderTime.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Climbing;

/// <remarks><see cref="Projects"/> is "tried, not completed", as older apps know it; <see cref="Marked"/> the projects chosen (ADR-067).</remarks>
public enum HistoryFilter { All, Completed, Projects, Marked }

/// <summary>One entry of climbing history. The boulder may be removed; history keeps it.</summary>
/// <param name="IsProject">Whether the person whose history this is marked the boulder as a project (ADR-067): theirs, not
/// the viewer's. Added for 1.1; older apps don't read it.</param>
public sealed record ClimbHistoryItemDto(BoulderSummaryDto Boulder, int Attempts, bool Completed, DateTimeOffset? CompletedAt, DateTimeOffset UpdatedAt, int? Rating,
    bool IsProject = false);

/// <param name="Projects">Tried and not completed, as it always was (older apps show it as "projects").</param>
/// <param name="MarkedProjects">
/// Projects the person chose, not yet completed, on boulders still on the wall (ADR-067). Added later: older apps don't
/// read it.
/// </param>
/// <param name="TriedThisMonth">Boulders with something recorded this month, completed or not (1.1).</param>
/// <param name="MarkedProjectsThisMonth">Of the open projects, those marked this month (1.1).</param>
public sealed record ClimbingStatsDto(int Completed, int Projects, int TotalAttempts, int CompletedThisMonth, int? MarkedProjects = null,
    int? TriedThisMonth = null, int? MarkedProjectsThisMonth = null);

/// <summary>Highest completed grade within ONE grading system (grades from different systems are never compared).</summary>
public sealed record HighestGradeDto(Guid GradeSystemId, string SystemName, GradeSystemType SystemType, string GymName, string Label, int Rank, string? ColorHex,
    string? ColorHex2 = null);

public sealed record WeekActivityDto(DateOnly WeekStart, int Completed);

/// <summary>
/// A gym where the person completed at least one boulder (ADR-064): how many distinct boulders, when the latest, and
/// the highest grade in each of that gym's scales — never compared across scales.
/// </summary>
public sealed record ClimbedGymDto(GymSummaryDto Gym, int Completed, DateTimeOffset? LastCompletedAt, IReadOnlyList<HighestGradeDto> Highest);

/// <summary>A distinction the person carries because they are staff of that gym — never because they follow it.</summary>
public sealed record StaffDistinctionDto(Guid GymId, string GymSlug, string GymName, GymRole Role, bool IsFoundingGym, bool IsEarlyPartner);

public sealed record ProfileDto(
    Guid Id, string DisplayName, string? AvatarUrl, DateTimeOffset MemberSince, bool IsMe,
    ClimbingStatsDto Stats, IReadOnlyList<HighestGradeDto> HighestGrades, IReadOnlyList<WeekActivityDto> Weekly,
    IReadOnlyList<GymSummaryDto> FollowedGyms, IReadOnlyList<ClimbHistoryItemDto> RecentCompletions,
    IReadOnlyList<StaffDistinctionDto> StaffDistinctions,
    /// <summary>Whether the viewer has blocked this person. Only the viewer ever sees it.</summary>
    bool IsBlocked = false,
    /// <summary>Where they have completed the most boulders; null before the first send.</summary>
    FavoriteSectorDto? FavoriteSector = null,
    /// <summary>The person keeps the content of their profile private (ADR-061). Added later: older apps don't read it.</summary>
    bool IsPrivate = false,
    /// <summary>
    /// True when the profile is private and the viewer may not see its content: then statistics, history, gyms and the
    /// rest come empty (never left out, so older apps still read the answer), and only name and photo are real.
    /// </summary>
    bool ContentHidden = false,
    /// <summary>The sections the person chose to show, in their order (ADR-061). Null from older servers.</summary>
    IReadOnlyList<Users.ProfileSectionDto>? Sections = null,
    /// <summary>
    /// The viewer's friendship with this person (ADR-063): none, asked, asked by them, friends. Null for one's own
    /// profile and when signed out. Added later: older apps don't read it.
    /// </summary>
    Users.FriendshipState? Friendship = null,
    /// <summary>
    /// The gyms where the person completed at least one boulder, latest first (ADR-064). Following a gym is not enough.
    /// Empty when the content is hidden. Added later: older apps don't read it.
    /// </summary>
    IReadOnlyList<ClimbedGymDto>? ClimbedGyms = null,
    /// <summary>
    /// What the person wrote about themselves. Like name and photo it says who they are, so a private profile shows it
    /// too. Null when they wrote none. Added later: older apps don't read it.
    /// </summary>
    string? Bio = null,
    /// <summary>
    /// How many friends the person has, counting those the viewer may see (as the list of friends does). Null when the
    /// content is hidden. Added later: older apps don't read it.
    /// </summary>
    int? FriendCount = null);

/// <summary>
/// The sector with the most boulders this person completed, each boulder once. With the gym's floor plan and the
/// sector's outline when the gym drew it, so the profile can show where it is.
/// </summary>
public sealed record FavoriteSectorDto(
    Guid SectorId, string SectorName, Guid GymId, string GymName, string GymSlug, int Completed,
    string? FloorPlanUrl, int? FloorPlanWidth, int? FloorPlanHeight, MapZoneDto? Zone);

public sealed record HomeGymDto(GymSummaryDto Gym, bool IsFavorite, int ActiveBoulders, int NewThisWeek);

public sealed record HomeDto(
    ClimbingStatsDto Stats, IReadOnlyList<HomeGymDto> Gyms,
    IReadOnlyList<BoulderSummaryDto> Projects, IReadOnlyList<BoulderSummaryDto> FreshToTry,
    IReadOnlyList<ClimbHistoryItemDto> RecentCompletions,
    IReadOnlyList<Notifications.AnnouncementDto> Updates,
    /// <summary>The projects the person chose, still on the wall and not completed, latest first (ADR-067). Added later.</summary>
    IReadOnlyList<BoulderSummaryDto>? MarkedProjects = null,
    /// <summary>
    /// Boulders tried, not completed, still on the wall and not marked as projects: what the app may suggest marking.
    /// Added later: older apps don't read it.
    /// </summary>
    int? UnmarkedTries = null);

/// <summary>Personal climbing history, statistics, public profiles and the Home screen. All computed on read.</summary>
public sealed class ActivityService(IAppDbContext db, ICurrentUser currentUser, IClock clock, BoulderReader reader, Notifications.AnnouncementService announcements, PartnerService partners, Users.BlockedPeople blocked, Users.ProfileAccess access, Users.FriendshipService friends)
{
    public const int Weeks = 12;

    public async Task<PagedResult<ClimbHistoryItemDto>> HistoryAsync(Guid userId, HistoryFilter? filter, int? page, int? pageSize, CancellationToken ct = default)
    {
        var user = await access.RequireVisibleAsync(userId, ct);
        var (p, size) = Paging.Normalize(page, pageSize, 20);
        // A private profile's history is not anyone else's to read: an empty page, as for someone who logged nothing.
        if (!await access.CanSeeContentAsync(user, ct)) return new PagedResult<ClimbHistoryItemDto>([], p, size, 0);
        var q = db.BoulderAttempts.AsNoTracking().OnExistingBoulders(db).Where(a => a.UserId == userId);
        q = filter switch
        {
            HistoryFilter.Completed => q.Where(a => a.Completed),
            HistoryFilter.Projects => q.Where(a => !a.Completed && a.Attempts > 0),
            HistoryFilter.Marked => q.Where(a => !a.Completed && db.BoulderProjects.Any(p => p.UserId == userId && p.BoulderId == a.BoulderId)),
            _ => q,
        };
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(a => a.CompletedAt ?? a.UpdatedAt).ThenByDescending(a => a.UpdatedAt)
            .Skip((p - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<ClimbHistoryItemDto>(await ToHistoryAsync(userId, rows, ct), p, size, total);
    }

    public async Task<ProfileDto> ProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await access.RequireVisibleAsync(userId, ct);
        var isPrivate = user.ProfileVisibility == ProfileVisibility.Private;
        var sections = Users.ProfileLayout.Read(user.ProfileSections);
        var friendship = await friends.StateWithAsync(userId, ct);
        if (!await access.CanSeeContentAsync(user, ct))
        {
            // Only what identifies the person: name and photo. Everything else empty, with the same shape as always.
            return new ProfileDto(user.Id, user.DisplayName, user.AvatarUrl, user.CreatedAt, false,
                new ClimbingStatsDto(0, 0, 0, 0), [], [], [], [], [],
                await blocked.ViewerBlockedAsync(userId, ct), null, isPrivate, ContentHidden: true, Sections: sections, Friendship: friendship, ClimbedGyms: [], Bio: user.Bio);
        }
        var gyms = await db.GymFollows.AsNoTracking().Where(f => f.UserId == userId)
            .Join(db.Gyms, f => f.GymId, g => g.Id, (f, g) => new { f.IsFavorite, g })
            .Where(x => x.g.Status == GymStatus.Active)
            .OrderByDescending(x => x.IsFavorite).ThenBy(x => x.g.Name)
            .Select(x => x.g).ToListAsync(ct);
        var recent = await db.BoulderAttempts.AsNoTracking().OnExistingBoulders(db).Where(a => a.UserId == userId && a.Completed)
            .OrderByDescending(a => a.CompletedAt).Take(6).ToListAsync(ct);

        return new ProfileDto(user.Id, user.DisplayName, user.AvatarUrl, user.CreatedAt, currentUser.UserId == userId,
            await StatsAsync(userId, ct), await HighestGradesAsync(userId, ct), await WeeklyAsync(userId, ct),
            await SummariesAsync(gyms, ct), await ToHistoryAsync(userId, recent, ct), await StaffDistinctionsAsync(userId, ct),
            await blocked.ViewerBlockedAsync(userId, ct), await FavoriteSectorAsync(userId, ct), isPrivate, false, sections, friendship, await ClimbedGymsAsync(userId, ct),
            user.Bio, await friends.CountOfAsync(userId, ct));
    }

    public async Task<HomeDto> HomeAsync(CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var now = clock.UtcNow;
        var weekAgo = now.AddDays(-7);

        var follows = await db.GymFollows.AsNoTracking().Where(f => f.UserId == userId)
            .Join(db.Gyms, f => f.GymId, g => g.Id, (f, g) => new { f.IsFavorite, g })
            .Where(x => x.g.Status == GymStatus.Active)
            .Select(x => new
            {
                x.IsFavorite,
                x.g,
                Active = db.Boulders.Count(b => b.GymId == x.g.Id && b.Status == BoulderStatus.Active),
                Fresh = db.Boulders.Count(b => b.GymId == x.g.Id && b.Status == BoulderStatus.Active && b.CreatedAt >= weekAgo),
            })
            .ToListAsync(ct);
        var gymIds = follows.Select(x => x.g.Id).ToList();

        // Projects: tried but not sent, still on the wall — anywhere the climber has been.
        var projects = await db.BoulderAttempts.AsNoTracking().OnExistingBoulders(db)
            .Where(a => a.UserId == userId && !a.Completed && a.Attempts > 0)
            .Join(db.Boulders, a => a.BoulderId, b => b.Id, (a, b) => new { a.UpdatedAt, b })
            .Where(x => x.b.Status == BoulderStatus.Active)
            .OrderByDescending(x => x.UpdatedAt).Take(8).Select(x => x.b).ToListAsync(ct);

        // Fresh to try: newest untried boulders at followed gyms, favourites first.
        var favoriteIds = follows.Where(x => x.IsFavorite).Select(x => x.g.Id).ToList();
        var fresh = await db.Boulders.AsNoTracking()
            .Where(b => gymIds.Contains(b.GymId) && b.Status == BoulderStatus.Active
                        && !db.BoulderAttempts.Any(a => a.BoulderId == b.Id && a.UserId == userId))
            .OrderByDescending(b => favoriteIds.Contains(b.GymId)).ThenByDescending(b => b.CreatedAt)
            .Take(8).ToListAsync(ct);

        var recent = await db.BoulderAttempts.AsNoTracking().OnExistingBoulders(db).Where(a => a.UserId == userId && a.Completed)
            .OrderByDescending(a => a.CompletedAt).Take(5).ToListAsync(ct);

        // The projects chosen (ADR-067): still on the wall, not completed, latest marked first.
        var marked = await OpenProjects(userId)
            .OrderByDescending(x => x.CreatedAt).Take(8).Select(x => x.Boulder).ToListAsync(ct);
        var unmarkedTries = await db.BoulderAttempts.AsNoTracking().OnExistingBoulders(db)
            .Where(a => a.UserId == userId && !a.Completed && a.Attempts > 0)
            .Where(a => !db.BoulderProjects.Any(p => p.UserId == userId && p.BoulderId == a.BoulderId))
            .Join(db.Boulders, a => a.BoulderId, b => b.Id, (a, b) => b)
            .CountAsync(b => b.Status == BoulderStatus.Active, ct);

        return new HomeDto(
            await StatsAsync(userId, ct),
            await HomeGymsAsync(follows.Select(x => (x.g, x.IsFavorite, x.Active, x.Fresh)).ToList(), ct),
            await reader.SummariesAsync(projects, ct),
            await reader.SummariesAsync(fresh, ct),
            await ToHistoryAsync(userId, recent, ct),
            await announcements.ForFollowedGymsAsync(userId, 5, ct),
            await reader.SummariesAsync(marked, ct),
            unmarkedTries);
    }

    /// <summary>A person's chosen projects that are still open: boulder on the wall, not completed.</summary>
    private IQueryable<OpenProject> OpenProjects(Guid userId) =>
        db.BoulderProjects.AsNoTracking().Where(p => p.UserId == userId)
            .Where(p => !db.BoulderAttempts.Any(a => a.UserId == userId && a.BoulderId == p.BoulderId && a.Completed))
            .Join(db.Boulders.Where(b => b.Status == BoulderStatus.Active), p => p.BoulderId, b => b.Id,
                (p, b) => new OpenProject { CreatedAt = p.CreatedAt, Boulder = b });

    // Built with an initializer, not a constructor, so the database can still order and count by it.
    private sealed class OpenProject
    {
        public DateTimeOffset CreatedAt { get; init; }
        public Domain.Boulders.Boulder Boulder { get; init; } = null!;
    }

    private async Task<IReadOnlyList<GymSummaryDto>> SummariesAsync(List<Domain.Gyms.Gym> gyms, CancellationToken ct)
    {
        var early = await partners.ActiveEarlyPartnerIdsAsync(gyms.Select(g => g.Id).ToList(), ct);
        return gyms.Select(g => GymSummaryDto.From(g, early.Contains(g.Id))).ToList();
    }

    private async Task<IReadOnlyList<HomeGymDto>> HomeGymsAsync(List<(Domain.Gyms.Gym Gym, bool IsFavorite, int Active, int Fresh)> rows, CancellationToken ct)
    {
        var early = await partners.ActiveEarlyPartnerIdsAsync(rows.Select(r => r.Gym.Id).ToList(), ct);
        return rows.OrderByDescending(r => r.IsFavorite).ThenBy(r => r.Gym.Name)
            .Select(r => new HomeGymDto(GymSummaryDto.From(r.Gym, early.Contains(r.Gym.Id)), r.IsFavorite, r.Active, r.Fresh)).ToList();
    }

    /// <summary>
    /// Distinctions a person shows on their profile: derived live from their staff roles, so they disappear by
    /// themselves when the person leaves the staff or the gym loses the status.
    /// </summary>
    private async Task<IReadOnlyList<StaffDistinctionDto>> StaffDistinctionsAsync(Guid userId, CancellationToken ct)
    {
        var memberships = await db.GymStaff.AsNoTracking().Where(m => m.UserId == userId)
            .Join(db.Gyms, m => m.GymId, g => g.Id, (m, g) => new { m.Role, Gym = g })
            .Where(x => x.Gym.Status == GymStatus.Active)
            .ToListAsync(ct);
        if (memberships.Count == 0) return [];
        var early = await partners.ActiveEarlyPartnerIdsAsync(memberships.Select(x => x.Gym.Id).ToList(), ct);
        return memberships
            .Where(x => x.Gym.IsFoundingGym || early.Contains(x.Gym.Id))
            .OrderByDescending(x => x.Gym.IsFoundingGym).ThenBy(x => x.Gym.Name)
            .Select(x => new StaffDistinctionDto(x.Gym.Id, x.Gym.Slug, x.Gym.Name, x.Role, x.Gym.IsFoundingGym, early.Contains(x.Gym.Id)))
            .ToList();
    }

    internal async Task<ClimbingStatsDto> StatsAsync(Guid userId, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var mine = db.BoulderAttempts.AsNoTracking().OnExistingBoulders(db).Where(a => a.UserId == userId);
        return new ClimbingStatsDto(
            await mine.CountAsync(a => a.Completed, ct),
            await mine.CountAsync(a => !a.Completed && a.Attempts > 0, ct),
            await mine.SumAsync(a => a.Attempts, ct),
            await mine.CountAsync(a => a.Completed && a.CompletedAt >= monthStart, ct),
            await OpenProjects(userId).CountAsync(ct),
            // Attempts are kept as one running total per boulder, so only the boulders can be counted by month, not the
            // attempts: a boulder counts when its progress changed this month (an attempt added, a send).
            await mine.CountAsync(a => (a.Completed || a.Attempts > 0) && (a.UpdatedAt >= monthStart || a.CompletedAt >= monthStart), ct),
            await OpenProjects(userId).CountAsync(p => p.CreatedAt >= monthStart, ct));
    }

    internal async Task<IReadOnlyList<HighestGradeDto>> HighestGradesAsync(Guid userId, CancellationToken ct)
    {
        var rows = await db.BoulderAttempts.AsNoTracking().OnExistingBoulders(db)
            .Where(a => a.UserId == userId && a.Completed)
            .Join(db.BoulderGrades, a => a.BoulderId, g => g.BoulderId, (a, g) => g)
            .Where(g => g.Source == GradeSource.Staff)
            .Join(db.GradeValues, g => g.GradeValueId, v => v.Id, (g, v) => v)
            .Join(db.GradeSystems, v => v.GradeSystemId, s => s.Id, (v, s) => new { v, s })
            .Join(db.Gyms, x => x.s.GymId, gym => (Guid?)gym.Id, (x, gym) => new { x.v, x.s, GymName = gym.Name })
            .ToListAsync(ct);

        return rows
            .GroupBy(x => x.s.Id)
            .Select(g => g.OrderByDescending(x => x.v.Rank).First())
            .OrderBy(x => TypeOrder(x.s.Type)).ThenByDescending(x => x.v.Rank)
            .Take(6)
            .Select(x => new HighestGradeDto(x.s.Id, x.s.Name, x.s.Type, x.GymName, x.v.Label, x.v.Rank, x.v.ColorHex, x.v.ColorHex2))
            .ToList();
    }

    private static int TypeOrder(GradeSystemType t) => t switch
    {
        GradeSystemType.Fontainebleau => 0, GradeSystemType.VScale => 1, GradeSystemType.Color => 2, _ => 3,
    };

    /// <summary>
    /// The gyms where the person completed at least one boulder (ADR-064). A send is a completed attempt row, and there
    /// is one row per person and boulder, so counting rows counts distinct boulders; attempts and projects don't count.
    /// The latest send orders them, sends without a date (none are recorded that way today) last. The highest grade
    /// follows the rule of the profile's highest grades — the staff's grade, by rank within one scale — per gym and
    /// scale. Only gyms open on BoulderTime, as for the gyms followed.
    /// </summary>
    internal async Task<IReadOnlyList<ClimbedGymDto>> ClimbedGymsAsync(Guid userId, CancellationToken ct)
    {
        var sends = await db.BoulderAttempts.AsNoTracking().OnExistingBoulders(db)
            .Where(a => a.UserId == userId && a.Completed)
            .Join(db.Boulders, a => a.BoulderId, b => b.Id, (a, b) => new { a.BoulderId, a.CompletedAt, b.GymId })
            .ToListAsync(ct);
        if (sends.Count == 0) return [];
        var gymIds = sends.Select(x => x.GymId).Distinct().ToList();
        var gyms = await db.Gyms.AsNoTracking().Where(g => gymIds.Contains(g.Id) && g.Status == GymStatus.Active).ToListAsync(ct);
        if (gyms.Count == 0) return [];

        var grades = await db.BoulderAttempts.AsNoTracking().OnExistingBoulders(db)
            .Where(a => a.UserId == userId && a.Completed)
            .Join(db.Boulders, a => a.BoulderId, b => b.Id, (a, b) => new { b.Id, b.GymId })
            .Join(db.BoulderGrades, x => x.Id, g => g.BoulderId, (x, g) => new { x.GymId, g })
            .Where(x => x.g.Source == GradeSource.Staff)
            .Join(db.GradeValues, x => x.g.GradeValueId, v => v.Id, (x, v) => new { x.GymId, v })
            .Join(db.GradeSystems, x => x.v.GradeSystemId, s => s.Id, (x, s) => new { x.GymId, x.v, s })
            .ToListAsync(ct);

        var early = await partners.ActiveEarlyPartnerIdsAsync(gyms.Select(g => g.Id).ToList(), ct);
        return gyms
            .Select(gym =>
            {
                var here = sends.Where(x => x.GymId == gym.Id).ToList();
                var highest = grades.Where(x => x.GymId == gym.Id)
                    .GroupBy(x => x.s.Id)
                    .Select(g => g.OrderByDescending(x => x.v.Rank).First())
                    .OrderBy(x => TypeOrder(x.s.Type)).ThenBy(x => x.s.Name)
                    .Select(x => new HighestGradeDto(x.s.Id, x.s.Name, x.s.Type, gym.Name, x.v.Label, x.v.Rank, x.v.ColorHex, x.v.ColorHex2))
                    .ToList();
                return new ClimbedGymDto(GymSummaryDto.From(gym, early.Contains(gym.Id)), here.Select(x => x.BoulderId).Distinct().Count(),
                    here.Max(x => x.CompletedAt), highest);
            })
            .OrderByDescending(x => x.LastCompletedAt.HasValue).ThenByDescending(x => x.LastCompletedAt).ThenBy(x => x.Gym.Name)
            .ToList();
    }

    internal async Task<IReadOnlyList<WeekActivityDto>> WeeklyAsync(Guid userId, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var thisWeek = today.AddDays(-(((int)today.DayOfWeek + 6) % 7)); // Monday
        var firstWeek = thisWeek.AddDays(-7 * (Weeks - 1));
        var from = new DateTimeOffset(firstWeek.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        var dates = await db.BoulderAttempts.AsNoTracking().OnExistingBoulders(db)
            .Where(a => a.UserId == userId && a.Completed && a.CompletedAt >= from)
            .Select(a => a.CompletedAt!.Value).ToListAsync(ct);
        return Enumerable.Range(0, Weeks).Select(i =>
        {
            var start = firstWeek.AddDays(7 * i);
            var s = new DateTimeOffset(start.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            return new WeekActivityDto(start, dates.Count(d => d >= s && d < s.AddDays(7)));
        }).ToList();
    }

    private async Task<IReadOnlyList<ClimbHistoryItemDto>> ToHistoryAsync(Guid userId, List<BoulderAttempt> rows, CancellationToken ct)
    {
        if (rows.Count == 0) return [];
        var ids = rows.Select(r => r.BoulderId).ToList();
        var boulders = await db.Boulders.AsNoTracking().Where(b => ids.Contains(b.Id)).ToListAsync(ct);
        var summaries = (await reader.SummariesAsync(boulders, ct)).ToDictionary(s => s.Id);
        var ratings = await db.BoulderRatings.AsNoTracking().Where(r => r.UserId == userId && ids.Contains(r.BoulderId))
            .ToDictionaryAsync(r => r.BoulderId, r => r.Rating, ct);
        var projects = await reader.ProjectsAsync(ids, userId, ct);
        return rows.Where(r => summaries.ContainsKey(r.BoulderId))
            .Select(r => new ClimbHistoryItemDto(summaries[r.BoulderId], r.Attempts, r.Completed, r.CompletedAt, r.UpdatedAt,
                ratings.TryGetValue(r.BoulderId, out var rating) ? rating : (int?)null, projects.Contains(r.BoulderId)))
            .ToList();
    }

    /// <summary>
    /// Counts completed boulders per sector. One progress row per person and boulder, so nothing counts twice, and an
    /// undone send no longer counts. Boulders taken down still count (they are the person's history); a boulder that
    /// staff moved counts in its current sector, since a move corrects a mistake. A tie goes to the sector of the most
    /// recent send. Only active gyms: a profile never names a gym that isn't public.
    /// </summary>
    public async Task<FavoriteSectorDto?> FavoriteSectorAsync(Guid userId, CancellationToken ct = default)
    {
        var top = await db.BoulderAttempts.AsNoTracking().OnExistingBoulders(db)
            .Where(a => a.UserId == userId && a.Completed)
            .Join(db.Boulders, a => a.BoulderId, b => b.Id, (a, b) => new { b.SectorId, b.GymId, a.CompletedAt })
            .Join(db.Gyms.Where(g => g.Status == GymStatus.Active), x => x.GymId, g => g.Id, (x, g) => x)
            .GroupBy(x => x.SectorId)
            .Select(g => new { SectorId = g.Key, Count = g.Count(), Last = g.Max(x => x.CompletedAt) })
            .OrderByDescending(x => x.Count).ThenByDescending(x => x.Last)
            .FirstOrDefaultAsync(ct);
        if (top is null) return null;
        var sector = await db.Sectors.AsNoTracking().FirstAsync(x => x.Id == top.SectorId, ct);
        var gym = await db.Gyms.AsNoTracking().FirstAsync(g => g.Id == sector.GymId, ct);
        return new FavoriteSectorDto(sector.Id, sector.Name, gym.Id, gym.Name, gym.Slug, top.Count,
            gym.FloorPlanUrl, gym.FloorPlanWidth, gym.FloorPlanHeight, MapZoneDto.FromJson(sector.MapZone));
    }
}
