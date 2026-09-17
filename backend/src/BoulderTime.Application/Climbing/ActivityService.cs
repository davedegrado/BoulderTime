using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Boulders;
using BoulderTime.Application.Common;
using BoulderTime.Application.Follows;
using BoulderTime.Application.Gyms;
using BoulderTime.Domain.Boulders;
using BoulderTime.Domain.Climbing;
using BoulderTime.Domain.Grading;
using BoulderTime.Domain.Gyms;
using BoulderTime.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Climbing;

public enum HistoryFilter { All, Completed, Projects }

/// <summary>One entry of climbing history. The boulder may be removed; history keeps it.</summary>
public sealed record ClimbHistoryItemDto(BoulderSummaryDto Boulder, int Attempts, bool Completed, DateTimeOffset? CompletedAt, DateTimeOffset UpdatedAt, int? Rating);

public sealed record ClimbingStatsDto(int Completed, int Projects, int TotalAttempts, int CompletedThisMonth);

/// <summary>Highest completed grade within ONE grading system (grades from different systems are never compared).</summary>
public sealed record HighestGradeDto(Guid GradeSystemId, string SystemName, GradeSystemType SystemType, string GymName, string Label, int Rank, string? ColorHex);

public sealed record WeekActivityDto(DateOnly WeekStart, int Completed);

public sealed record ProfileDto(
    Guid Id, string DisplayName, string? AvatarUrl, DateTimeOffset MemberSince, bool IsMe,
    ClimbingStatsDto Stats, IReadOnlyList<HighestGradeDto> HighestGrades, IReadOnlyList<WeekActivityDto> Weekly,
    IReadOnlyList<GymSummaryDto> FollowedGyms, IReadOnlyList<ClimbHistoryItemDto> RecentCompletions);

public sealed record HomeGymDto(GymSummaryDto Gym, bool IsFavorite, int ActiveBoulders, int NewThisWeek);

public sealed record HomeDto(
    ClimbingStatsDto Stats, IReadOnlyList<HomeGymDto> Gyms,
    IReadOnlyList<BoulderSummaryDto> Projects, IReadOnlyList<BoulderSummaryDto> FreshToTry,
    IReadOnlyList<ClimbHistoryItemDto> RecentCompletions);

/// <summary>Personal climbing history, statistics, public profiles and the Home screen. All computed on read.</summary>
public sealed class ActivityService(IAppDbContext db, ICurrentUser currentUser, IClock clock, BoulderReader reader)
{
    public const int Weeks = 12;

    public async Task<PagedResult<ClimbHistoryItemDto>> HistoryAsync(Guid userId, HistoryFilter? filter, int? page, int? pageSize, CancellationToken ct = default)
    {
        await RequireVisibleProfileAsync(userId, ct);
        var (p, size) = Paging.Normalize(page, pageSize, 20);
        var q = db.BoulderAttempts.AsNoTracking().Where(a => a.UserId == userId);
        q = filter switch
        {
            HistoryFilter.Completed => q.Where(a => a.Completed),
            HistoryFilter.Projects => q.Where(a => !a.Completed && a.Attempts > 0),
            _ => q,
        };
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(a => a.CompletedAt ?? a.UpdatedAt).ThenByDescending(a => a.UpdatedAt)
            .Skip((p - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<ClimbHistoryItemDto>(await ToHistoryAsync(userId, rows, ct), p, size, total);
    }

    public async Task<ProfileDto> ProfileAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await RequireVisibleProfileAsync(userId, ct);
        var gyms = await db.GymFollows.AsNoTracking().Where(f => f.UserId == userId)
            .Join(db.Gyms, f => f.GymId, g => g.Id, (f, g) => new { f.IsFavorite, g })
            .Where(x => x.g.Status == GymStatus.Active)
            .OrderByDescending(x => x.IsFavorite).ThenBy(x => x.g.Name)
            .Select(x => x.g).ToListAsync(ct);
        var recent = await db.BoulderAttempts.AsNoTracking().Where(a => a.UserId == userId && a.Completed)
            .OrderByDescending(a => a.CompletedAt).Take(6).ToListAsync(ct);

        return new ProfileDto(user.Id, user.DisplayName, user.AvatarUrl, user.CreatedAt, currentUser.UserId == userId,
            await StatsAsync(userId, ct), await HighestGradesAsync(userId, ct), await WeeklyAsync(userId, ct),
            gyms.Select(GymSummaryDto.From).ToList(), await ToHistoryAsync(userId, recent, ct));
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
        var projects = await db.BoulderAttempts.AsNoTracking()
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

        var recent = await db.BoulderAttempts.AsNoTracking().Where(a => a.UserId == userId && a.Completed)
            .OrderByDescending(a => a.CompletedAt).Take(5).ToListAsync(ct);

        return new HomeDto(
            await StatsAsync(userId, ct),
            follows.OrderByDescending(x => x.IsFavorite).ThenBy(x => x.g.Name)
                .Select(x => new HomeGymDto(GymSummaryDto.From(x.g), x.IsFavorite, x.Active, x.Fresh)).ToList(),
            await reader.SummariesAsync(projects, ct),
            await reader.SummariesAsync(fresh, ct),
            await ToHistoryAsync(userId, recent, ct));
    }

    internal async Task<ClimbingStatsDto> StatsAsync(Guid userId, CancellationToken ct)
    {
        var now = clock.UtcNow;
        var monthStart = new DateTimeOffset(now.Year, now.Month, 1, 0, 0, 0, TimeSpan.Zero);
        var mine = db.BoulderAttempts.AsNoTracking().Where(a => a.UserId == userId);
        return new ClimbingStatsDto(
            await mine.CountAsync(a => a.Completed, ct),
            await mine.CountAsync(a => !a.Completed && a.Attempts > 0, ct),
            await mine.SumAsync(a => a.Attempts, ct),
            await mine.CountAsync(a => a.Completed && a.CompletedAt >= monthStart, ct));
    }

    internal async Task<IReadOnlyList<HighestGradeDto>> HighestGradesAsync(Guid userId, CancellationToken ct)
    {
        var rows = await db.BoulderAttempts.AsNoTracking()
            .Where(a => a.UserId == userId && a.Completed)
            .Join(db.BoulderGrades, a => a.BoulderId, g => g.BoulderId, (a, g) => g)
            .Where(g => g.Source == GradeSource.Staff)
            .Join(db.GradeValues, g => g.GradeValueId, v => v.Id, (g, v) => v)
            .Join(db.GradeSystems, v => v.GradeSystemId, s => s.Id, (v, s) => new { v, s })
            .Join(db.Gyms, x => x.s.GymId, gym => gym.Id, (x, gym) => new { x.v, x.s, GymName = gym.Name })
            .ToListAsync(ct);

        static int TypeOrder(GradeSystemType t) => t switch
        {
            GradeSystemType.Fontainebleau => 0, GradeSystemType.VScale => 1, GradeSystemType.Color => 2, _ => 3,
        };
        return rows
            .GroupBy(x => x.s.Id)
            .Select(g => g.OrderByDescending(x => x.v.Rank).First())
            .OrderBy(x => TypeOrder(x.s.Type)).ThenByDescending(x => x.v.Rank)
            .Take(6)
            .Select(x => new HighestGradeDto(x.s.Id, x.s.Name, x.s.Type, x.GymName, x.v.Label, x.v.Rank, x.v.ColorHex))
            .ToList();
    }

    internal async Task<IReadOnlyList<WeekActivityDto>> WeeklyAsync(Guid userId, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(clock.UtcNow.UtcDateTime);
        var thisWeek = today.AddDays(-(((int)today.DayOfWeek + 6) % 7)); // Monday
        var firstWeek = thisWeek.AddDays(-7 * (Weeks - 1));
        var from = new DateTimeOffset(firstWeek.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);

        var dates = await db.BoulderAttempts.AsNoTracking()
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
        return rows.Where(r => summaries.ContainsKey(r.BoulderId))
            .Select(r => new ClimbHistoryItemDto(summaries[r.BoulderId], r.Attempts, r.Completed, r.CompletedAt, r.UpdatedAt,
                ratings.TryGetValue(r.BoulderId, out var rating) ? rating : (int?)null))
            .ToList();
    }

    private async Task<User> RequireVisibleProfileAsync(Guid userId, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, ct) ?? throw new NotFoundException("User", userId);
        // Profiles are public by default; this is where future privacy settings plug in.
        if (user.ProfileVisibility != ProfileVisibility.Public && currentUser.UserId != userId) throw new NotFoundException("User", userId);
        return user;
    }
}
