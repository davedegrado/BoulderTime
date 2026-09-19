using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Common;
using BoulderTime.Domain.Gyms;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Gyms;

public sealed record SetFoundingGymRequest(bool? IsFoundingGym);
public sealed record StartEarlyPartnerRequest(DateTimeOffset? StartedAt, string? Note);
public sealed record EarlyPartnerDto(Guid Id, Guid GymId, DateTimeOffset StartedAt, DateTimeOffset? EndedAt, string? Note, bool IsActive);
public sealed record PartnerGymDto(GymSummaryDto Gym, EarlyPartnerDto? EarlyPartner);

/// <summary>
/// Platform-level distinctions: the founding gym (one, historical) and early partners (several, commercial period).
/// Both are reserved to platform admins — gym owners, admins and staff cannot grant themselves either status.
/// </summary>
public sealed class PartnerService(IAppDbContext db, GymAccess access, ICurrentUser currentUser, IClock clock)
{
    /// <summary>Which of these gyms are early partners right now, in one query.</summary>
    public async Task<HashSet<Guid>> ActiveEarlyPartnerIdsAsync(IReadOnlyCollection<Guid> gymIds, CancellationToken ct = default)
    {
        if (gymIds.Count == 0) return [];
        var now = clock.UtcNow;
        var ids = await db.EarlyPartnerships.AsNoTracking()
            .Where(p => gymIds.Contains(p.GymId) && p.StartedAt <= now && (p.EndedAt == null || p.EndedAt > now))
            .Select(p => p.GymId).ToListAsync(ct);
        return ids.ToHashSet();
    }

    public async Task<bool> IsEarlyPartnerAsync(Guid gymId, CancellationToken ct = default) =>
        (await ActiveEarlyPartnerIdsAsync([gymId], ct)).Contains(gymId);

    public async Task<EarlyPartnerDto?> CurrentPartnershipAsync(Guid gymId, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var partnership = await db.EarlyPartnerships.AsNoTracking()
            .Where(p => p.GymId == gymId && p.StartedAt <= now && (p.EndedAt == null || p.EndedAt > now))
            .OrderByDescending(p => p.StartedAt).FirstOrDefaultAsync(ct);
        return partnership is null ? null : ToDto(partnership, now);
    }

    /// <summary>
    /// Designates or clears the founding gym. Designating while another gym already holds it fails instead of
    /// silently moving the distinction: the admin must clear the current one first.
    /// </summary>
    public async Task<GymDetailDto> SetFoundingGymAsync(Guid gymId, SetFoundingGymRequest r, CancellationToken ct = default)
    {
        await access.RequirePlatformAdminAsync(ct);
        if (r.IsFoundingGym is not { } isFounding) throw new ValidationException("isFoundingGym", "Say whether this gym is the founding gym.");
        var gym = await db.Gyms.FirstOrDefaultAsync(g => g.Id == gymId, ct) ?? throw new NotFoundException("Gym", gymId);

        if (isFounding)
        {
            var current = await db.Gyms.AsNoTracking().FirstOrDefaultAsync(g => g.IsFoundingGym && g.Id != gymId, ct);
            if (current is not null)
                throw new ConflictException($"{current.Name} is already the founding gym. Remove that first — there can only be one.", "founding_gym_exists");
        }

        gym.SetFoundingGym(isFounding);
        try { await db.SaveChangesAsync(ct); }
        catch (UniqueConstraintViolationException) { throw new ConflictException("Another gym is already the founding gym.", "founding_gym_exists"); }
        return GymDetailDto.From(gym, Domain.Staff.GymRole.Owner) with
        {
            IsEarlyPartner = await IsEarlyPartnerAsync(gymId, ct),
            EarlyPartnerSince = (await CurrentPartnershipAsync(gymId, ct))?.StartedAt,
        };
    }

    public async Task<EarlyPartnerDto> StartEarlyPartnerAsync(Guid gymId, StartEarlyPartnerRequest r, CancellationToken ct = default)
    {
        await access.RequirePlatformAdminAsync(ct);
        var userId = currentUser.RequireUserId();
        _ = await db.Gyms.AsNoTracking().FirstOrDefaultAsync(g => g.Id == gymId, ct) ?? throw new NotFoundException("Gym", gymId);
        if (Input.Trimmed(r.Note).Length > EarlyPartnership.NoteMaxLength)
            throw new ValidationException("note", $"Keep the note to {EarlyPartnership.NoteMaxLength} characters or fewer.");

        var now = clock.UtcNow;
        if (await CurrentPartnershipAsync(gymId, ct) is { } existing) return existing;

        var partnership = EarlyPartnership.Start(gymId, userId, r.StartedAt ?? now, r.Note, now);
        db.EarlyPartnerships.Add(partnership);
        await db.SaveChangesAsync(ct);
        return ToDto(partnership, now);
    }

    /// <summary>Ends the running partnership. The record is kept, so the history stays readable.</summary>
    public async Task<EarlyPartnerDto?> EndEarlyPartnerAsync(Guid gymId, CancellationToken ct = default)
    {
        await access.RequirePlatformAdminAsync(ct);
        var now = clock.UtcNow;
        var partnership = await db.EarlyPartnerships
            .Where(p => p.GymId == gymId && (p.EndedAt == null || p.EndedAt > now))
            .OrderByDescending(p => p.StartedAt).FirstOrDefaultAsync(ct);
        if (partnership is null) return null;
        partnership.End(now);
        await db.SaveChangesAsync(ct);
        return ToDto(partnership, now);
    }

    /// <summary>Everything the admin screen needs: current early partners and the founding gym.</summary>
    public async Task<IReadOnlyList<PartnerGymDto>> ListAsync(CancellationToken ct = default)
    {
        await access.RequirePlatformAdminAsync(ct);
        var now = clock.UtcNow;
        var partnerships = await db.EarlyPartnerships.AsNoTracking()
            .Where(p => p.EndedAt == null || p.EndedAt > now)
            .OrderByDescending(p => p.StartedAt).ToListAsync(ct);
        var gymIds = partnerships.Select(p => p.GymId).ToList();
        var gyms = await db.Gyms.AsNoTracking().Where(g => gymIds.Contains(g.Id) || g.IsFoundingGym).ToListAsync(ct);
        var byGym = partnerships.GroupBy(p => p.GymId).ToDictionary(g => g.Key, g => g.First());

        return gyms
            .OrderByDescending(g => g.IsFoundingGym).ThenBy(g => g.Name)
            .Select(g => new PartnerGymDto(
                GymSummaryDto.From(g, byGym.ContainsKey(g.Id)),
                byGym.TryGetValue(g.Id, out var p) ? ToDto(p, now) : null))
            .ToList();
    }

    private static EarlyPartnerDto ToDto(EarlyPartnership p, DateTimeOffset now) =>
        new(p.Id, p.GymId, p.StartedAt, p.EndedAt, p.Note, p.IsActiveAt(now));
}
