using BoulderTime.Application.Abstractions;
using BoulderTime.Domain.Badges;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Badges;

/// <param name="Days">Active days added, reconstructed from the dates already recorded.</param>
/// <param name="Awards">Badges awarded, by badge id.</param>
public sealed record BackfillReport(int People, int Days, IReadOnlyDictionary<string, int> Awards);

/// <summary>
/// The badges people earned before badges existed (ADR-069), run once from the server (<c>backfill-badges</c>).
/// Active days are rebuilt from the dates the database already holds — when an attempt row was created, last changed
/// and completed, when a project was marked, a boulder rated, a grade or an intensity voted: real days of activity,
/// fewer than there were, never more. Then every count badge reached is awarded, dated where the data says when.
/// Nobody is notified. Safe to run again: what exists is left alone.
/// </summary>
public sealed class BadgeBackfill(IAppDbContext db, BadgeAwarder awarder)
{
    public async Task<BackfillReport> RunAsync(CancellationToken ct = default)
    {
        var people = await db.Users.AsNoTracking().Where(u => u.SuspendedAt == null && u.DeletionRequestedAt == null).Select(u => u.Id).ToListAsync(ct);

        var instants = new List<(Guid UserId, DateTimeOffset At)>();
        foreach (var a in await db.BoulderAttempts.AsNoTracking().Select(a => new { a.UserId, a.CreatedAt, a.UpdatedAt, a.CompletedAt }).ToListAsync(ct))
        {
            instants.Add((a.UserId, a.CreatedAt));
            instants.Add((a.UserId, a.UpdatedAt));
            if (a.CompletedAt is { } c) instants.Add((a.UserId, c));
        }
        instants.AddRange((await db.BoulderProjects.AsNoTracking().Select(p => new { p.UserId, p.CreatedAt }).ToListAsync(ct)).Select(x => (x.UserId, x.CreatedAt)));
        instants.AddRange((await db.BoulderRatings.AsNoTracking().Select(p => new { p.UserId, p.CreatedAt }).ToListAsync(ct)).Select(x => (x.UserId, x.CreatedAt)));
        instants.AddRange((await db.GradeSuggestions.AsNoTracking().Select(p => new { p.UserId, p.CreatedAt }).ToListAsync(ct)).Select(x => (x.UserId, x.CreatedAt)));
        instants.AddRange((await db.IntensityVotes.AsNoTracking().Select(p => new { p.UserId, p.CreatedAt }).ToListAsync(ct)).Select(x => (x.UserId, x.CreatedAt)));

        var existing = (await db.ActivityDays.AsNoTracking().Select(d => new { d.UserId, d.Day }).ToListAsync(ct)).Select(d => (d.UserId, d.Day)).ToHashSet();
        var wanted = people.ToHashSet();
        var added = instants.Where(x => wanted.Contains(x.UserId)).Select(x => (x.UserId, Day: ItalianTime.Day(x.At))).Distinct()
            .Where(x => !existing.Contains(x)).ToList();
        db.ActivityDays.AddRange(added.Select(x => ActivityDay.Of(x.UserId, x.Day)));
        await db.SaveChangesAsync(ct);

        var awards = new Dictionary<string, int>();
        foreach (var userId in people)
            foreach (var award in await awarder.EvaluateAsync(userId, dated: true, notify: false, ct))
                awards[award.BadgeId] = awards.GetValueOrDefault(award.BadgeId) + 1;
        return new BackfillReport(people.Count, added.Count, awards);
    }
}
