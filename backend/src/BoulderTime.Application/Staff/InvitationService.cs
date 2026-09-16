using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Common;
using BoulderTime.Domain.Gyms;
using BoulderTime.Domain.Staff;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Staff;

/// <summary>Invitee side: see, accept and decline invitations sent to your sign-in email.</summary>
public sealed class InvitationService(IAppDbContext db, ICurrentUser currentUser, IClock clock)
{
    public async Task<IReadOnlyList<MyInvitationDto>> ListMineAsync(CancellationToken ct = default)
    {
        var email = await MyEmailAsync(ct);
        var now = clock.UtcNow;
        // Order before projecting: EF Core can't translate ordering on constructor-built records.
        var rows = await db.StaffInvitations.AsNoTracking()
            .Where(i => i.Email == email && i.Status == InvitationStatus.Pending && i.ExpiresAt > now)
            .Join(db.Gyms, i => i.GymId, g => g.Id, (i, g) => new { i, g })
            .Where(x => x.g.Status != GymStatus.Archived)
            .Join(db.Users, x => x.i.InvitedByUserId, u => u.Id, (x, u) => new { x.i, x.g, Inviter = u.DisplayName })
            .OrderBy(x => x.i.ExpiresAt)
            .ToListAsync(ct);
        return rows.Select(x => new MyInvitationDto(x.i.Id, x.g.Id, x.g.Slug, x.g.Name, x.g.City, x.i.Role, x.Inviter, x.i.ExpiresAt)).ToList();
    }

    public async Task<MyStaffGymDto> AcceptAsync(Guid invitationId, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var invitation = await LoadMineAsync(invitationId, ct);
        var now = clock.UtcNow;

        var member = await db.GymStaff.FirstOrDefaultAsync(s => s.GymId == invitation.GymId && s.UserId == userId, ct);
        if (member is null)
            db.GymStaff.Add(member = GymStaffMember.Create(invitation.GymId, userId, invitation.Role, invitation.InvitedByUserId, now));
        else if (invitation.Role > member.Role)
            member.ChangeRole(invitation.Role); // accepting never lowers an existing role

        invitation.Accept(userId, now);
        await db.SaveChangesAsync(ct);

        var gym = await db.Gyms.AsNoTracking().FirstAsync(g => g.Id == invitation.GymId, ct);
        return new MyStaffGymDto(gym.Id, gym.Slug, gym.Name, gym.City, gym.LogoUrl, member.Role);
    }

    public async Task DeclineAsync(Guid invitationId, CancellationToken ct = default)
    {
        var invitation = await LoadMineAsync(invitationId, ct);
        invitation.Decline(clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<MyStaffGymDto>> ListMyStaffGymsAsync(CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var rows = await db.GymStaff.AsNoTracking()
            .Where(s => s.UserId == userId)
            .Join(db.Gyms, s => s.GymId, g => g.Id, (s, g) => new { s.Role, g })
            .OrderBy(x => x.g.Name)
            .ToListAsync(ct);
        return rows.Select(x => new MyStaffGymDto(x.g.Id, x.g.Slug, x.g.Name, x.g.City, x.g.LogoUrl, x.Role)).ToList();
    }

    /// <summary>Only the addressee can see or act on an invitation; everyone else gets 404.</summary>
    private async Task<StaffInvitation> LoadMineAsync(Guid invitationId, CancellationToken ct)
    {
        var email = await MyEmailAsync(ct);
        var invitation = await db.StaffInvitations.FirstOrDefaultAsync(i => i.Id == invitationId && i.Email == email, ct)
                         ?? throw new NotFoundException("Invitation", invitationId);
        if (!invitation.IsOpen(clock.UtcNow))
            throw new ConflictException("This invitation has expired or was already answered.", "invitation_closed");
        return invitation;
    }

    private async Task<string> MyEmailAsync(CancellationToken ct)
    {
        var userId = currentUser.RequireUserId();
        return await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.Email).FirstAsync(ct);
    }
}
