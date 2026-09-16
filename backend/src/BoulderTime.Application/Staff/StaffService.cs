using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Common;
using BoulderTime.Application.Gyms;
using BoulderTime.Domain.Staff;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Staff;

/// <summary>
/// Gym-side staff management. Permission matrix:
///   view staff & invitations: STAFF+
///   invite / revoke / change role / remove: ADMIN+, and only for roles the actor may grant/manage
///   (admins never touch owners; only owners create owners). A gym always keeps at least one owner.
/// </summary>
public sealed class StaffService(IAppDbContext db, GymAccess access, ICurrentUser currentUser, IClock clock)
{
    public async Task<IReadOnlyList<StaffMemberDto>> ListMembersAsync(Guid gymId, CancellationToken ct = default)
    {
        await access.RequireRoleAsync(gymId, GymRole.Staff, ct);
        var rows = await db.GymStaff.AsNoTracking()
            .Where(s => s.GymId == gymId)
            .Join(db.Users, s => s.UserId, u => u.Id, (s, u) => new { s, u })
            .ToListAsync(ct);
        return rows
            .OrderByDescending(r => r.s.Role).ThenBy(r => r.u.DisplayName)
            .Select(r => new StaffMemberDto(r.u.Id, r.u.DisplayName, r.u.Email, r.u.AvatarUrl, r.s.Role, r.s.CreatedAt))
            .ToList();
    }

    public async Task<IReadOnlyList<GymInvitationDto>> ListPendingInvitationsAsync(Guid gymId, CancellationToken ct = default)
    {
        await access.RequireRoleAsync(gymId, GymRole.Staff, ct);
        var now = clock.UtcNow;
        var rows = await db.StaffInvitations.AsNoTracking()
            .Where(i => i.GymId == gymId && i.Status == InvitationStatus.Pending && i.ExpiresAt > now)
            .Join(db.Users, i => i.InvitedByUserId, u => u.Id, (i, u) => new { i, u.DisplayName })
            .OrderByDescending(x => x.i.CreatedAt)
            .ToListAsync(ct);
        return rows.Select(x => ToDto(x.i, x.DisplayName)).ToList();
    }

    public async Task<GymInvitationDto> InviteAsync(Guid gymId, InviteStaffRequest r, CancellationToken ct = default)
    {
        var (_, actorRole) = await access.RequireRoleAsync(gymId, GymRole.Admin, ct);
        new Validator()
            .Check(Input.IsEmail(r.Email), "email", "Enter a valid email address.")
            .Check(r.Role is not null && Enum.IsDefined(r.Role.Value), "role", "Choose a role.")
            .ThrowIfInvalid();
        return await CreateInvitationAsync(gymId, r.Email!, r.Role!.Value, actorRole, ct);
    }

    /// <summary>Shared by gym admins and platform admins (who invite a gym's first owner).</summary>
    internal async Task<GymInvitationDto> CreateInvitationAsync(Guid gymId, string email, GymRole role, GymRole actorRole, CancellationToken ct)
    {
        if (!actorRole.CanGrant(role))
            throw new ForbiddenException("You can't invite someone with that role.", "role_not_grantable");

        var actorId = currentUser.RequireUserId();
        var now = clock.UtcNow;
        var normalized = email.Trim().ToLowerInvariant();

        var alreadyStaff = await db.GymStaff
            .Join(db.Users, s => s.UserId, u => u.Id, (s, u) => new { s.GymId, u.Email })
            .AnyAsync(x => x.GymId == gymId && x.Email == normalized, ct);
        if (alreadyStaff) throw new ConflictException("This person is already on the staff.", "already_staff");

        var existing = await db.StaffInvitations
            .FirstOrDefaultAsync(i => i.GymId == gymId && i.Email == normalized && i.Status == InvitationStatus.Pending, ct);
        if (existing is not null)
        {
            if (existing.IsOpen(now)) throw new ConflictException("There's already an open invitation for this email.", "invitation_pending");
            existing.MarkExpired(now);
            await db.SaveChangesAsync(ct); // free the unique (gym, email) pending slot before inserting the new invitation
        }

        var invitation = StaffInvitation.Create(gymId, normalized, role, actorId, now);
        db.StaffInvitations.Add(invitation);
        try { await db.SaveChangesAsync(ct); }
        catch (UniqueConstraintViolationException) { throw new ConflictException("There's already an open invitation for this email.", "invitation_pending"); }

        var inviter = await db.Users.AsNoTracking().Where(u => u.Id == actorId).Select(u => u.DisplayName).FirstAsync(ct);
        return ToDto(invitation, inviter);
    }

    public async Task RevokeInvitationAsync(Guid gymId, Guid invitationId, CancellationToken ct = default)
    {
        var (_, actorRole) = await access.RequireRoleAsync(gymId, GymRole.Admin, ct);
        var invitation = await db.StaffInvitations.FirstOrDefaultAsync(i => i.Id == invitationId && i.GymId == gymId, ct)
                         ?? throw new NotFoundException("Invitation", invitationId);
        if (!actorRole.CanGrant(invitation.Role))
            throw new ForbiddenException("You can't revoke this invitation.", "role_not_grantable");
        if (invitation.Status != InvitationStatus.Pending)
            throw new ConflictException("This invitation is no longer open.", "invitation_closed");
        invitation.Revoke(clock.UtcNow);
        await db.SaveChangesAsync(ct);
    }

    public async Task<StaffMemberDto> ChangeRoleAsync(Guid gymId, Guid userId, ChangeRoleRequest r, CancellationToken ct = default)
    {
        var (_, actorRole) = await access.RequireRoleAsync(gymId, GymRole.Admin, ct);
        if (r.Role is not { } newRole || !Enum.IsDefined(newRole)) throw new ValidationException("role", "Choose a role.");

        var member = await db.GymStaff.FirstOrDefaultAsync(s => s.GymId == gymId && s.UserId == userId, ct)
                     ?? throw new NotFoundException("Staff member", userId);
        if (!actorRole.CanManage(member.Role) || !actorRole.CanGrant(newRole))
            throw new ForbiddenException("You can't change this person's role.", "role_not_manageable");
        if (member.Role == GymRole.Owner && newRole != GymRole.Owner)
            await EnsureAnotherOwnerAsync(gymId, userId, ct);

        member.ChangeRole(newRole);
        await db.SaveChangesAsync(ct);
        var u = await db.Users.AsNoTracking().FirstAsync(x => x.Id == userId, ct);
        return new StaffMemberDto(u.Id, u.DisplayName, u.Email, u.AvatarUrl, member.Role, member.CreatedAt);
    }

    /// <summary>Removes a member. Any member may remove themselves (leave), except the last owner.</summary>
    public async Task RemoveAsync(Guid gymId, Guid userId, CancellationToken ct = default)
    {
        var actorId = currentUser.RequireUserId();
        var leaving = actorId == userId;
        var (_, actorRole) = await access.RequireRoleAsync(gymId, leaving ? GymRole.Staff : GymRole.Admin, ct);

        var member = await db.GymStaff.FirstOrDefaultAsync(s => s.GymId == gymId && s.UserId == userId, ct)
                     ?? throw new NotFoundException("Staff member", userId);
        if (!leaving && !actorRole.CanManage(member.Role))
            throw new ForbiddenException("You can't remove this person.", "role_not_manageable");
        if (member.Role == GymRole.Owner)
            await EnsureAnotherOwnerAsync(gymId, userId, ct);

        db.GymStaff.Remove(member);
        await db.SaveChangesAsync(ct);
    }

    private async Task EnsureAnotherOwnerAsync(Guid gymId, Guid exceptUserId, CancellationToken ct)
    {
        var others = await db.GymStaff.CountAsync(s => s.GymId == gymId && s.Role == GymRole.Owner && s.UserId != exceptUserId, ct);
        if (others == 0)
            throw new ConflictException("A gym needs at least one owner. Make someone else an owner first.", "last_owner");
    }

    private static GymInvitationDto ToDto(StaffInvitation i, string inviter) =>
        new(i.Id, i.Email, i.Role, i.Status, i.CreatedAt, i.ExpiresAt, inviter);
}
