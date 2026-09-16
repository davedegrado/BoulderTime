using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Common;
using BoulderTime.Domain.Gyms;
using BoulderTime.Domain.Staff;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Gyms;

/// <summary>
/// The single place gym permissions are decided. Roles are always read from the database.
/// Platform admins are treated as owners of every gym.
/// </summary>
public sealed class GymAccess(IAppDbContext db, ICurrentUser currentUser)
{
    public async Task<bool> IsPlatformAdminAsync(CancellationToken ct = default)
    {
        if (currentUser.UserId is not { } id) return false;
        return await db.Users.AnyAsync(u => u.Id == id && u.IsPlatformAdmin, ct);
    }

    public async Task RequirePlatformAdminAsync(CancellationToken ct = default)
    {
        currentUser.RequireUserId();
        if (!await IsPlatformAdminAsync(ct))
            throw new ForbiddenException("Only BoulderTime administrators can do this.", "platform_admin_required");
    }

    /// <summary>The caller's effective role at a gym, or null. Platform admins resolve to Owner.</summary>
    public async Task<GymRole?> GetRoleAsync(Guid gymId, CancellationToken ct = default)
    {
        if (currentUser.UserId is not { } id) return null;
        if (await IsPlatformAdminAsync(ct)) return GymRole.Owner;
        var member = await db.GymStaff.AsNoTracking().FirstOrDefaultAsync(s => s.GymId == gymId && s.UserId == id, ct);
        return member?.Role;
    }

    /// <summary>
    /// Loads a gym (tracked) and requires the caller to hold at least <paramref name="minimum"/> there.
    /// Non-members get 404 for non-public gyms (no existence leak) and 403 for public ones.
    /// </summary>
    public async Task<(Gym Gym, GymRole Role)> RequireRoleAsync(Guid gymId, GymRole minimum, CancellationToken ct = default)
    {
        currentUser.RequireUserId();
        var gym = await db.Gyms.FirstOrDefaultAsync(g => g.Id == gymId, ct) ?? throw new NotFoundException("Gym", gymId);
        var role = await GetRoleAsync(gymId, ct);
        if (role is null)
        {
            if (!gym.IsPubliclyVisible) throw new NotFoundException("Gym", gymId);
            throw new ForbiddenException("Only this gym's staff can do this.", "gym_staff_required");
        }
        if (!role.Value.AtLeast(minimum))
            throw new ForbiddenException($"This needs the {minimum.ToString().ToUpperInvariant()} role at this gym.", "gym_role_too_low");
        return (gym, role.Value);
    }
}
