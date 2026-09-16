using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Common;
using BoulderTime.Application.Gyms;
using BoulderTime.Domain.Candidates;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Candidates;

public sealed record GymCandidateDto(
    Guid Id, string GymName, string City, string? OfficialEmail, string? Website, string? Notes,
    GymCandidateStatus Status, DateTimeOffset CreatedAt, DateTimeOffset? HandledAt, Guid? GymId,
    string? SubmittedBy, string? SubmittedByEmail);

public sealed record SubmitGymCandidateRequest(string? GymName, string? City, string? OfficialEmail, string? Website, string? Notes);
public sealed record UpdateCandidateStatusRequest(GymCandidateStatus? Status);

public sealed class GymCandidateService(IAppDbContext db, GymAccess access, ICurrentUser currentUser, IClock clock)
{
    /// <summary>Cap on open suggestions per user, to keep the admin queue free of spam.</summary>
    public const int MaxOpenPerUser = 5;

    public async Task<GymCandidateDto> SubmitAsync(SubmitGymCandidateRequest r, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        new Validator()
            .Check(Input.Trimmed(r.GymName).Length >= 2, "gymName", "Enter the gym's name.")
            .Check(Input.MaxLength(r.GymName, GymCandidate.NameMaxLength), "gymName", $"Keep it to {GymCandidate.NameMaxLength} characters or fewer.")
            .Check(Input.Trimmed(r.City).Length >= 2, "city", "Enter the city.")
            .Check(Input.MaxLength(r.City, 100), "city", "Keep it to 100 characters or fewer.")
            .Check(Input.OptionalEmail(r.OfficialEmail), "officialEmail", "Enter a valid email address.")
            .Check(Input.OptionalUrl(r.Website), "website", "Enter a full web address starting with https://")
            .Check(Input.MaxLength(r.Notes, GymCandidate.NotesMaxLength), "notes", $"Keep it to {GymCandidate.NotesMaxLength} characters or fewer.")
            .ThrowIfInvalid();

        var open = await db.GymCandidates.CountAsync(c => c.SubmittedByUserId == userId &&
            (c.Status == GymCandidateStatus.Pending || c.Status == GymCandidateStatus.Contacted), ct);
        if (open >= MaxOpenPerUser)
            throw new ConflictException($"You already have {MaxOpenPerUser} open suggestions. We'll get to them soon.", "too_many_open_candidates");

        var candidate = GymCandidate.Submit(userId, r.GymName!, r.City!, r.OfficialEmail, r.Website, r.Notes, clock.UtcNow);
        db.GymCandidates.Add(candidate);
        await db.SaveChangesAsync(ct);
        return ToDto(candidate, null, null);
    }

    public async Task<IReadOnlyList<GymCandidateDto>> ListMineAsync(CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var rows = await db.GymCandidates.AsNoTracking()
            .Where(c => c.SubmittedByUserId == userId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync(ct);
        return rows.Select(c => ToDto(c, null, null)).ToList();
    }

    public async Task<PagedResult<GymCandidateDto>> AdminListAsync(GymCandidateStatus? status, int? page, int? pageSize, CancellationToken ct = default)
    {
        await access.RequirePlatformAdminAsync(ct);
        var (p, size) = Paging.Normalize(page, pageSize);
        var q = db.GymCandidates.AsNoTracking().AsQueryable();
        if (status is { } s) q = q.Where(c => c.Status == s);

        var total = await q.CountAsync(ct);
        var rows = await q.OrderBy(c => c.Status).ThenByDescending(c => c.CreatedAt)
            .Skip((p - 1) * size).Take(size)
            .Join(db.Users, c => c.SubmittedByUserId, u => u.Id, (c, u) => new { c, u.DisplayName, u.Email })
            .ToListAsync(ct);
        return new PagedResult<GymCandidateDto>(rows.Select(x => ToDto(x.c, x.DisplayName, x.Email)).ToList(), p, size, total);
    }

    public async Task<GymCandidateDto> AdminSetStatusAsync(Guid candidateId, UpdateCandidateStatusRequest r, CancellationToken ct = default)
    {
        await access.RequirePlatformAdminAsync(ct);
        if (r.Status is not { } status || !Enum.IsDefined(status)) throw new ValidationException("status", "Choose a status.");
        var candidate = await db.GymCandidates.FirstOrDefaultAsync(c => c.Id == candidateId, ct) ?? throw new NotFoundException("Gym suggestion", candidateId);
        if (candidate.GymId is not null && status != GymCandidateStatus.Accepted)
            throw new ConflictException("A gym was already created from this suggestion.", "candidate_has_gym");

        candidate.SetStatus(status, currentUser.RequireUserId(), clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return ToDto(candidate, null, null);
    }

    private static GymCandidateDto ToDto(GymCandidate c, string? by, string? byEmail) =>
        new(c.Id, c.GymName, c.City, c.OfficialEmail, c.Website, c.Notes, c.Status, c.CreatedAt, c.HandledAt, c.GymId, by, byEmail);
}
