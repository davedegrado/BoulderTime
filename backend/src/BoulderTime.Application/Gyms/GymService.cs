using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Common;
using BoulderTime.Domain.Gyms;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Gyms;

public sealed class GymService(IAppDbContext db, GymAccess access, ICurrentUser currentUser)
{
    /// <summary>Public discovery: active gyms only, matched on name or city.</summary>
    public async Task<PagedResult<GymSummaryDto>> SearchAsync(string? query, int? page, int? pageSize, CancellationToken ct = default)
    {
        var (p, size) = Paging.Normalize(page, pageSize);
        var q = db.Gyms.AsNoTracking().Where(g => g.Status == GymStatus.Active);

        var text = Input.Trimmed(query);
        if (text.Length > 0)
        {
            var pattern = Input.LikeContains(text.ToLowerInvariant());
            q = q.Where(g => EF.Functions.Like(g.Name.ToLower(), pattern, @"\") || EF.Functions.Like(g.City.ToLower(), pattern, @"\"));
        }

        var total = await q.CountAsync(ct);
        var items = await q.OrderBy(g => g.Name).Skip((p - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<GymSummaryDto>(items.Select(GymSummaryDto.From).ToList(), p, size, total);
    }

    /// <summary>Active gyms are public. Draft/archived gyms are visible only to their staff and platform admins.</summary>
    public async Task<GymDetailDto> GetBySlugAsync(string slug, CancellationToken ct = default)
    {
        var normalized = slug.Trim().ToLowerInvariant();
        var gym = await db.Gyms.AsNoTracking().FirstOrDefaultAsync(g => g.Slug == normalized, ct)
                  ?? throw new NotFoundException("Gym", slug);
        var role = await access.GetRoleAsync(gym.Id, ct);
        if (!gym.IsPubliclyVisible && role is null) throw new NotFoundException("Gym", slug);

        Follows.GymFollowState? follow = null;
        if (currentUser.UserId is { } uid)
        {
            var f = await db.GymFollows.AsNoTracking().FirstOrDefaultAsync(x => x.GymId == gym.Id && x.UserId == uid, ct);
            follow = f is null ? new(false, false, false) : new(true, f.IsFavorite, f.NotificationsEnabled);
        }
        var followers = await db.GymFollows.CountAsync(x => x.GymId == gym.Id, ct);
        return GymDetailDto.From(gym, role) with { Follow = follow, FollowerCount = followers };
    }

    public async Task<GymDetailDto> UpdateAsync(Guid gymId, UpdateGymRequest r, CancellationToken ct = default)
    {
        var (gym, role) = await access.RequireRoleAsync(gymId, Domain.Staff.GymRole.Admin, ct);
        GymValidation.ValidateProfile(r.Name, r.City, r.Description, r.Address, r.Website, r.Email, r.Phone);
        gym.UpdateProfile(r.Name!, r.Description, r.Address, r.City!, r.Website, r.Email, r.Phone);
        await db.SaveChangesAsync(ct);
        return GymDetailDto.From(gym, role);
    }
}
