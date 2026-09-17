using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Boulders;
using BoulderTime.Application.Common;
using BoulderTime.Domain.Community;
using BoulderTime.Domain.Grading;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Community;

public sealed record ConsensusBucketDto(Guid GradeValueId, string Label, int Rank, string? ColorHex, int Votes);

public sealed record SystemConsensusDto(
    Guid GradeSystemId, string SystemName, GradeSystemType SystemType, int TotalVotes,
    Guid? ConsensusValueId, Guid? OfficialValueId, Guid? ViewerValueId, IReadOnlyList<ConsensusBucketDto> Buckets,
    IReadOnlyList<ConsensusBucketDto> Scale);

public sealed record GradeConsensusDto(bool ViewerCanSuggest, IReadOnlyList<SystemConsensusDto> Systems);

public sealed record SuggestGradeRequest(Guid? GradeSystemId, Guid? GradeValueId);

/// <summary>
/// How suggestions become a "community grade". Isolated so the rule can change without touching the service:
/// currently the most-voted value, ties broken toward the median of all votes, then the lower grade.
/// </summary>
public static class GradeConsensus
{
    public static Guid? Pick(IReadOnlyList<ConsensusBucketDto> buckets)
    {
        var voted = buckets.Where(b => b.Votes > 0).ToList();
        if (voted.Count == 0) return null;
        var max = voted.Max(b => b.Votes);
        var top = voted.Where(b => b.Votes == max).ToList();
        if (top.Count == 1) return top[0].GradeValueId;

        var ranks = voted.SelectMany(b => Enumerable.Repeat(b.Rank, b.Votes)).OrderBy(r => r).ToList();
        var median = ranks[(ranks.Count - 1) / 2];
        return top.OrderBy(b => Math.Abs(b.Rank - median)).ThenBy(b => b.Rank).First().GradeValueId;
    }
}

public sealed class GradeSuggestionService(IAppDbContext db, BoulderAccess boulders, ICurrentUser currentUser)
{
    public async Task<GradeConsensusDto> GetAsync(Guid boulderId, CancellationToken ct = default)
    {
        var scope = await boulders.RequireVisibleAsync(boulderId, ct);
        var gymId = scope.Gym.Id;
        var systems = await db.GradeSystems.AsNoTracking().Where(s => s.GymId == gymId && s.IsActive).OrderBy(s => s.SortOrder).ToListAsync(ct);
        var systemIds = systems.Select(s => s.Id).ToList();
        var values = await db.GradeValues.AsNoTracking().Where(v => systemIds.Contains(v.GradeSystemId)).OrderBy(v => v.Rank).ToListAsync(ct);
        var votes = await db.GradeSuggestions.AsNoTracking().Where(s => s.BoulderId == boulderId)
            .GroupBy(s => s.GradeValueId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var official = await db.BoulderGrades.AsNoTracking().Where(g => g.BoulderId == boulderId && g.Source == Domain.Boulders.GradeSource.Staff)
            .ToDictionaryAsync(g => g.GradeSystemId, g => g.GradeValueId, ct);
        var mine = currentUser.UserId is { } uid
            ? await db.GradeSuggestions.AsNoTracking().Where(s => s.BoulderId == boulderId && s.UserId == uid).ToDictionaryAsync(s => s.GradeSystemId, s => s.GradeValueId, ct)
            : [];

        var result = systems.Select(s =>
        {
            var scale = values.Where(v => v.GradeSystemId == s.Id && v.IsActive)
                .Select(v => new ConsensusBucketDto(v.Id, v.Label, v.Rank, v.ColorHex, votes.GetValueOrDefault(v.Id))).ToList();
            // Votes for since-retired values still count and are shown.
            var buckets = values.Where(v => v.GradeSystemId == s.Id && votes.ContainsKey(v.Id))
                .Select(v => new ConsensusBucketDto(v.Id, v.Label, v.Rank, v.ColorHex, votes[v.Id])).ToList();
            return new SystemConsensusDto(s.Id, s.Name, s.Type, buckets.Sum(b => b.Votes), GradeConsensus.Pick(buckets),
                official.TryGetValue(s.Id, out var o) ? o : null, mine.TryGetValue(s.Id, out var m) ? m : null, buckets, scale);
        }).ToList();

        return new GradeConsensusDto(await CanSuggestAsync(boulderId, ct), result);
    }

    public async Task<GradeConsensusDto> SuggestAsync(Guid boulderId, SuggestGradeRequest r, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var scope = await boulders.RequireVisibleAsync(boulderId, ct);
        if (!await CanSuggestAsync(boulderId, ct)) throw new ForbiddenException("Try the boulder before suggesting a grade.", "attempt_required");

        var ok = r.GradeSystemId is { } systemId && r.GradeValueId is { } valueId && await db.GradeValues.AsNoTracking()
            .Where(v => v.Id == valueId && v.IsActive && v.GradeSystemId == systemId)
            .Join(db.GradeSystems, v => v.GradeSystemId, s => s.Id, (v, s) => s)
            .AnyAsync(s => s.GymId == scope.Gym.Id && s.IsActive, ct);
        if (!ok) throw new ValidationException("gradeValueId", "Pick a grade from this gym's grading systems.");

        var existing = await db.GradeSuggestions.FirstOrDefaultAsync(s => s.BoulderId == boulderId && s.UserId == userId && s.GradeSystemId == r.GradeSystemId, ct);
        if (existing is null) db.GradeSuggestions.Add(GradeSuggestion.Create(boulderId, userId, r.GradeSystemId!.Value, r.GradeValueId!.Value));
        else existing.Change(r.GradeValueId!.Value);
        try { await db.SaveChangesAsync(ct); }
        catch (UniqueConstraintViolationException) { throw new ConflictException("You already suggested a grade in this system.", "suggestion_exists"); }
        return await GetAsync(boulderId, ct);
    }

    public async Task<GradeConsensusDto> WithdrawAsync(Guid boulderId, Guid gradeSystemId, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var existing = await db.GradeSuggestions.FirstOrDefaultAsync(s => s.BoulderId == boulderId && s.UserId == userId && s.GradeSystemId == gradeSystemId, ct);
        if (existing is not null)
        {
            db.GradeSuggestions.Remove(existing);
            await db.SaveChangesAsync(ct);
        }
        return await GetAsync(boulderId, ct);
    }

    private async Task<bool> CanSuggestAsync(Guid boulderId, CancellationToken ct) =>
        currentUser.UserId is { } uid && await db.BoulderAttempts.AnyAsync(a => a.BoulderId == boulderId && a.UserId == uid && a.Attempts > 0, ct);
}
