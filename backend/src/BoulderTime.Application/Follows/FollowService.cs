using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Common;
using BoulderTime.Application.Gyms;
using BoulderTime.Domain.Follows;
using BoulderTime.Domain.Gyms;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Follows;

public sealed record GymFollowRequest(bool? IsFavorite, bool? NotificationsEnabled);
public sealed record FollowRequest(bool? NotificationsEnabled);

public sealed record GymFollowState(bool IsFollowing, bool IsFavorite, bool NotificationsEnabled);
public sealed record FollowState(bool IsFollowing, bool NotificationsEnabled);

public sealed record FollowedGymDto(GymSummaryDto Gym, bool IsFavorite, bool NotificationsEnabled);
public sealed record FollowedSectorDto(Guid SectorId, string SectorName, Guid GymId, string GymSlug, string GymName, bool NotificationsEnabled);
public sealed record FollowedBoulderDto(Guid BoulderId, Guid GymId, string GymName, string SectorName, bool NotificationsEnabled);
public sealed record MyFollowsDto(IReadOnlyList<FollowedGymDto> Gyms, IReadOnlyList<FollowedSectorDto> Sectors, IReadOnlyList<FollowedBoulderDto> Boulders);

/// <summary>Follows are idempotent PUT/DELETE: following twice updates settings, unfollowing twice is a no-op.</summary>
public sealed class FollowService(IAppDbContext db, GymAccess access, ICurrentUser currentUser, IClock clock)
{
    public async Task<GymFollowState> FollowGymAsync(Guid gymId, GymFollowRequest r, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        await RequireVisibleGymAsync(gymId, ct);
        var follow = await db.GymFollows.FirstOrDefaultAsync(f => f.UserId == userId && f.GymId == gymId, ct);
        if (follow is null) db.GymFollows.Add(follow = GymFollow.Create(userId, gymId, clock.UtcNow));
        follow.Update(r.IsFavorite, r.NotificationsEnabled);
        await SaveIdempotentAsync(ct);
        return new GymFollowState(true, follow.IsFavorite, follow.NotificationsEnabled);
    }

    public async Task UnfollowGymAsync(Guid gymId, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var follow = await db.GymFollows.FirstOrDefaultAsync(f => f.UserId == userId && f.GymId == gymId, ct);
        if (follow is null) return;
        db.GymFollows.Remove(follow);
        await db.SaveChangesAsync(ct);
    }

    public async Task<FollowState> FollowSectorAsync(Guid sectorId, FollowRequest r, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var sector = await db.Sectors.AsNoTracking().FirstOrDefaultAsync(s => s.Id == sectorId, ct) ?? throw new NotFoundException("Sector", sectorId);
        await RequireVisibleGymAsync(sector.GymId, ct);
        var follow = await db.SectorFollows.FirstOrDefaultAsync(f => f.UserId == userId && f.SectorId == sectorId, ct);
        if (follow is null) db.SectorFollows.Add(follow = SectorFollow.Create(userId, sectorId, clock.UtcNow));
        follow.SetNotifications(r.NotificationsEnabled);
        await SaveIdempotentAsync(ct);
        return new FollowState(true, follow.NotificationsEnabled);
    }

    public async Task UnfollowSectorAsync(Guid sectorId, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var follow = await db.SectorFollows.FirstOrDefaultAsync(f => f.UserId == userId && f.SectorId == sectorId, ct);
        if (follow is null) return;
        db.SectorFollows.Remove(follow);
        await db.SaveChangesAsync(ct);
    }

    public async Task<FollowState> FollowBoulderAsync(Guid boulderId, FollowRequest r, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var boulder = await db.Boulders.AsNoTracking().FirstOrDefaultAsync(b => b.Id == boulderId, ct) ?? throw new NotFoundException("Boulder", boulderId);
        await RequireVisibleGymAsync(boulder.GymId, ct);
        var follow = await db.BoulderFollows.FirstOrDefaultAsync(f => f.UserId == userId && f.BoulderId == boulderId, ct);
        if (follow is null) db.BoulderFollows.Add(follow = BoulderFollow.Create(userId, boulderId, clock.UtcNow));
        follow.SetNotifications(r.NotificationsEnabled);
        await SaveIdempotentAsync(ct);
        return new FollowState(true, follow.NotificationsEnabled);
    }

    public async Task UnfollowBoulderAsync(Guid boulderId, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var follow = await db.BoulderFollows.FirstOrDefaultAsync(f => f.UserId == userId && f.BoulderId == boulderId, ct);
        if (follow is null) return;
        db.BoulderFollows.Remove(follow);
        await db.SaveChangesAsync(ct);
    }

    public async Task<MyFollowsDto> ListMineAsync(CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var gyms = await db.GymFollows.AsNoTracking().Where(f => f.UserId == userId)
            .Join(db.Gyms, f => f.GymId, g => g.Id, (f, g) => new { f, g })
            .Where(x => x.g.Status == GymStatus.Active)
            .ToListAsync(ct);
        var sectors = await db.SectorFollows.AsNoTracking().Where(f => f.UserId == userId)
            .Join(db.Sectors, f => f.SectorId, s => s.Id, (f, s) => new { f, s })
            .Join(db.Gyms, x => x.s.GymId, g => g.Id, (x, g) => new { x.f, x.s, g })
            .ToListAsync(ct);
        var boulders = await db.BoulderFollows.AsNoTracking().Where(f => f.UserId == userId)
            .Join(db.Boulders, f => f.BoulderId, b => b.Id, (f, b) => new { f, b })
            .Join(db.Gyms, x => x.b.GymId, g => g.Id, (x, g) => new { x.f, x.b, g })
            .Join(db.Sectors, x => x.b.SectorId, s => s.Id, (x, s) => new { x.f, x.b, x.g, s })
            .ToListAsync(ct);

        return new MyFollowsDto(
            gyms.OrderByDescending(x => x.f.IsFavorite).ThenBy(x => x.g.Name)
                .Select(x => new FollowedGymDto(GymSummaryDto.From(x.g), x.f.IsFavorite, x.f.NotificationsEnabled)).ToList(),
            sectors.OrderBy(x => x.g.Name).ThenBy(x => x.s.SortOrder)
                .Select(x => new FollowedSectorDto(x.s.Id, x.s.Name, x.g.Id, x.g.Slug, x.g.Name, x.f.NotificationsEnabled)).ToList(),
            boulders.OrderByDescending(x => x.f.CreatedAt)
                .Select(x => new FollowedBoulderDto(x.b.Id, x.g.Id, x.g.Name, x.s.Name, x.f.NotificationsEnabled)).ToList());
    }

    private async Task RequireVisibleGymAsync(Guid gymId, CancellationToken ct)
    {
        var gym = await db.Gyms.AsNoTracking().FirstOrDefaultAsync(g => g.Id == gymId, ct) ?? throw new NotFoundException("Gym", gymId);
        if (!gym.IsPubliclyVisible && await access.GetRoleAsync(gymId, ct) is null) throw new NotFoundException("Gym", gymId);
    }

    private async Task SaveIdempotentAsync(CancellationToken ct)
    {
        // Two taps racing to create the same follow: the loser's insert hits the unique index; the follow exists either way.
        try { await db.SaveChangesAsync(ct); }
        catch (UniqueConstraintViolationException) { }
    }
}
