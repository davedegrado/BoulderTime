using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Common;
using BoulderTime.Domain.Boulders;
using BoulderTime.Domain.Climbing;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Climbing;

public sealed record ProjectStateDto(bool IsProject);

/// <summary>
/// The boulders a climber chose as projects (ADR-067). Only the climber marks and unmarks them; nothing does it for
/// them. A boulder off the wall can't become a project, but one that already was can always be unmarked.
/// </summary>
public sealed class ProjectService(IAppDbContext db, ICurrentUser currentUser, IClock clock, Gyms.GymAccess access, IBadgeQueue badges)
{
    public async Task<ProjectStateDto> MarkAsync(Guid boulderId, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var status = await VisibleStatusAsync(boulderId, ct);
        if (await db.BoulderProjects.AnyAsync(p => p.UserId == userId && p.BoulderId == boulderId, ct)) return new(true);
        if (status != BoulderStatus.Active)
            throw new ConflictException("This boulder is no longer on the wall: it can't become a project.", "boulder_removed");
        db.BoulderProjects.Add(BoulderProject.Mark(userId, boulderId, clock.UtcNow));
        try { await db.SaveChangesAsync(ct); }
        catch (UniqueConstraintViolationException) { /* marked at the same moment from another device: it is marked */ }
        badges.Enqueue(userId, clock.UtcNow);
        return new(true);
    }

    public async Task<ProjectStateDto> UnmarkAsync(Guid boulderId, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var row = await db.BoulderProjects.FirstOrDefaultAsync(p => p.UserId == userId && p.BoulderId == boulderId, ct);
        if (row is not null)
        {
            db.BoulderProjects.Remove(row);
            await db.SaveChangesAsync(ct);
        }
        return new(false);
    }

    /// <summary>The boulder's status, if the climber may see it; deleted boulders and hidden gyms don't exist.</summary>
    private async Task<BoulderStatus> VisibleStatusAsync(Guid boulderId, CancellationToken ct)
    {
        var b = await db.Boulders.AsNoTracking().Where(x => x.Id == boulderId && x.Status != BoulderStatus.Deleted)
            .Join(db.Gyms, x => x.GymId, g => g.Id, (x, g) => new { x.Status, GymId = g.Id, GymStatus = g.Status })
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Boulder", boulderId);
        if (b.GymStatus != Domain.Gyms.GymStatus.Active && await access.GetRoleAsync(b.GymId, ct) is null)
            throw new NotFoundException("Boulder", boulderId);
        return b.Status;
    }
}
