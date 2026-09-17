using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Common;
using BoulderTime.Application.Gyms;
using BoulderTime.Application.Staff;
using BoulderTime.Domain.Candidates;
using BoulderTime.Domain.Common;
using BoulderTime.Domain.Gyms;
using BoulderTime.Domain.Staff;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Admin;

public sealed record AdminDashboardDto(int TotalGyms, int ActiveGyms, int PendingGymCandidates, int Users, int PendingReports);

public sealed record AdminGymDto(Guid Id, string Slug, string Name, string City, GymStatus Status, int StaffCount, int OwnerCount, int PendingInvitations, DateTimeOffset CreatedAt);

public sealed record AdminUserDto(Guid Id, string DisplayName, string Email, bool IsPlatformAdmin, int StaffGyms, DateTimeOffset CreatedAt);

public sealed record CreateGymRequest(string? Name, string? City, string? Address, string? Website, string? Email, string? Phone, string? Description, Guid? CandidateId);
public sealed record SetGymStatusRequest(GymStatus? Status);
public sealed record InviteOwnerRequest(string? Email);

public sealed class AdminService(IAppDbContext db, GymAccess access, StaffService staff, ICurrentUser currentUser, IClock clock)
{
    public async Task<AdminDashboardDto> GetDashboardAsync(CancellationToken ct = default)
    {
        await access.RequirePlatformAdminAsync(ct);
        return new AdminDashboardDto(
            await db.Gyms.CountAsync(ct),
            await db.Gyms.CountAsync(g => g.Status == GymStatus.Active, ct),
            await db.GymCandidates.CountAsync(c => c.Status == GymCandidateStatus.Pending, ct),
            await db.Users.CountAsync(ct),
            await db.Reports.CountAsync(r => r.Status == Domain.Community.ReportStatus.Pending, ct));
    }

    public async Task<PagedResult<AdminGymDto>> ListGymsAsync(string? query, GymStatus? status, int? page, int? pageSize, CancellationToken ct = default)
    {
        await access.RequirePlatformAdminAsync(ct);
        var (p, size) = Paging.Normalize(page, pageSize);
        var now = clock.UtcNow;
        var q = db.Gyms.AsNoTracking().AsQueryable();
        if (status is { } s) q = q.Where(g => g.Status == s);
        var text = Input.Trimmed(query);
        if (text.Length > 0)
        {
            var pattern = Input.LikeContains(text.ToLowerInvariant());
            q = q.Where(g => EF.Functions.Like(g.Name.ToLower(), pattern, @"\") || EF.Functions.Like(g.City.ToLower(), pattern, @"\"));
        }

        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(g => g.CreatedAt).Skip((p - 1) * size).Take(size)
            .Select(g => new
            {
                g,
                StaffCount = db.GymStaff.Count(m => m.GymId == g.Id),
                OwnerCount = db.GymStaff.Count(m => m.GymId == g.Id && m.Role == GymRole.Owner),
                Pending = db.StaffInvitations.Count(i => i.GymId == g.Id && i.Status == InvitationStatus.Pending && i.ExpiresAt > now),
            })
            .ToListAsync(ct);
        var items = rows.Select(x => new AdminGymDto(x.g.Id, x.g.Slug, x.g.Name, x.g.City, x.g.Status, x.StaffCount, x.OwnerCount, x.Pending, x.g.CreatedAt)).ToList();
        return new PagedResult<AdminGymDto>(items, p, size, total);
    }

    /// <summary>Creates a gym in Draft. When created from a suggestion, the suggestion is marked Accepted and linked.</summary>
    public async Task<GymDetailDto> CreateGymAsync(CreateGymRequest r, CancellationToken ct = default)
    {
        await access.RequirePlatformAdminAsync(ct);
        GymValidation.ValidateProfile(r.Name, r.City, r.Description, r.Address, r.Website, r.Email, r.Phone);

        GymCandidate? candidate = null;
        if (r.CandidateId is { } candidateId)
        {
            candidate = await db.GymCandidates.FirstOrDefaultAsync(c => c.Id == candidateId, ct) ?? throw new NotFoundException("Gym suggestion", candidateId);
            if (candidate.GymId is not null) throw new ConflictException("A gym was already created from this suggestion.", "candidate_has_gym");
        }

        var gym = Gym.Create(r.Name!, await UniqueSlugAsync(r.Name!, ct), r.City!);
        gym.UpdateProfile(r.Name!, r.Description, r.Address, r.City!, r.Website, r.Email, r.Phone);
        db.Gyms.Add(gym);
        if (candidate is not null)
        {
            candidate.SetStatus(GymCandidateStatus.Accepted, currentUser.RequireUserId(), clock.UtcNow);
            candidate.LinkGym(gym.Id);
        }
        await db.SaveChangesAsync(ct);
        return GymDetailDto.From(gym, GymRole.Owner);
    }

    public async Task<GymDetailDto> SetGymStatusAsync(Guid gymId, SetGymStatusRequest r, CancellationToken ct = default)
    {
        await access.RequirePlatformAdminAsync(ct);
        if (r.Status is not { } status || !Enum.IsDefined(status)) throw new ValidationException("status", "Choose a status.");
        var gym = await db.Gyms.FirstOrDefaultAsync(g => g.Id == gymId, ct) ?? throw new NotFoundException("Gym", gymId);
        gym.SetStatus(status);
        await db.SaveChangesAsync(ct);
        return GymDetailDto.From(gym, GymRole.Owner);
    }

    /// <summary>Assigns a gym's owner through the normal invitation flow, so the person explicitly accepts.</summary>
    public async Task<GymInvitationDto> InviteOwnerAsync(Guid gymId, InviteOwnerRequest r, CancellationToken ct = default)
    {
        await access.RequirePlatformAdminAsync(ct);
        if (!Input.IsEmail(r.Email)) throw new ValidationException("email", "Enter a valid email address.");
        if (!await db.Gyms.AnyAsync(g => g.Id == gymId, ct)) throw new NotFoundException("Gym", gymId);
        return await staff.CreateInvitationAsync(gymId, r.Email!, GymRole.Owner, GymRole.Owner, ct);
    }

    public async Task<PagedResult<AdminUserDto>> ListUsersAsync(string? query, int? page, int? pageSize, CancellationToken ct = default)
    {
        await access.RequirePlatformAdminAsync(ct);
        var (p, size) = Paging.Normalize(page, pageSize);
        var q = db.Users.AsNoTracking().AsQueryable();
        var text = Input.Trimmed(query);
        if (text.Length > 0)
        {
            var pattern = Input.LikeContains(text.ToLowerInvariant());
            q = q.Where(u => EF.Functions.Like(u.DisplayName.ToLower(), pattern, @"\") || EF.Functions.Like(u.Email.ToLower(), pattern, @"\"));
        }
        var total = await q.CountAsync(ct);
        var rows = await q.OrderByDescending(u => u.CreatedAt).Skip((p - 1) * size).Take(size)
            .Select(u => new { u, StaffGyms = db.GymStaff.Count(s => s.UserId == u.Id) })
            .ToListAsync(ct);
        return new PagedResult<AdminUserDto>(
            rows.Select(x => new AdminUserDto(x.u.Id, x.u.DisplayName, x.u.Email, x.u.IsPlatformAdmin, x.StaffGyms, x.u.CreatedAt)).ToList(), p, size, total);
    }

    private async Task<string> UniqueSlugAsync(string name, CancellationToken ct)
    {
        var baseSlug = Slug.From(name);
        var taken = await db.Gyms.Where(g => g.Slug == baseSlug || g.Slug.StartsWith(baseSlug + "-")).Select(g => g.Slug).ToListAsync(ct);
        if (!taken.Contains(baseSlug)) return baseSlug;
        for (var i = 2; ; i++)
        {
            var candidate = $"{baseSlug}-{i}";
            if (!taken.Contains(candidate)) return candidate;
        }
    }
}
