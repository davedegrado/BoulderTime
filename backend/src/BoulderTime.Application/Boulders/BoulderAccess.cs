using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Common;
using BoulderTime.Application.Gyms;
using BoulderTime.Domain.Boulders;
using BoulderTime.Domain.Gyms;
using BoulderTime.Domain.Staff;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Boulders;

public sealed record BoulderScope(Boulder Boulder, Gym Gym, GymRole? ViewerRole)
{
    public bool IsStaff => ViewerRole is not null;
}

/// <summary>Resolves a boulder together with its gym and the caller's role, applying gym visibility rules once.</summary>
public sealed class BoulderAccess(IAppDbContext db, GymAccess access)
{
    /// <summary>Anyone may see boulders of active gyms; staff and platform admins also see their non-public gyms.</summary>
    public async Task<BoulderScope> RequireVisibleAsync(Guid boulderId, CancellationToken ct)
    {
        var boulder = await db.Boulders.AsNoTracking().FirstOrDefaultAsync(b => b.Id == boulderId, ct) ?? throw new NotFoundException("Boulder", boulderId);
        var gym = await db.Gyms.AsNoTracking().FirstAsync(g => g.Id == boulder.GymId, ct);
        var role = await access.GetRoleAsync(gym.Id, ct);
        if (!gym.IsPubliclyVisible && role is null) throw new NotFoundException("Boulder", boulderId);
        return new BoulderScope(boulder, gym, role);
    }

    public async Task<BoulderScope> RequireStaffAsync(Guid boulderId, GymRole minimum, CancellationToken ct)
    {
        var scope = await RequireVisibleAsync(boulderId, ct);
        if (scope.ViewerRole is not { } role || !role.AtLeast(minimum))
            throw new ForbiddenException("Only this gym's staff can do this.", "gym_staff_required");
        return scope;
    }
}
