using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Boulders;
using BoulderTime.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Climbing;

public sealed record CompletionDto(PersonDto Person, DateTimeOffset? CompletedAt);

/// <param name="Total">Everyone who completed it, as the card counts them.</param>
/// <param name="People">The ones this viewer may see by name; the rest are "and N others".</param>
public sealed record BoulderCompletionsDto(int Total, PagedResult<CompletionDto> People);

/// <summary>
/// Who completed a boulder (ADR-047). The number is public, like the rest of the card; the names are for signed-in
/// climbers only. Left out of the names, never out of the number: people who asked not to appear in leaderboards and
/// lists, people blocked either way, suspended accounts (those are out of the number too). The viewer always sees
/// themself.
/// </summary>
public sealed class CompletionService(IAppDbContext db, BoulderAccess boulders, BoulderReader reader, Users.BlockedPeople blocked, ICurrentUser currentUser)
{
    public async Task<BoulderCompletionsDto> ListAsync(Guid boulderId, int? page, int? pageSize, CancellationToken ct = default)
    {
        var viewer = currentUser.RequireUserId();
        await boulders.RequireVisibleAsync(boulderId, ct);
        var total = (await reader.CompletionsAsync([boulderId], ct)).GetValueOrDefault(boulderId);

        var hidden = await blocked.ForViewerAsync(ct);
        var (p, size) = Paging.Normalize(page, pageSize, 30);
        var query = db.BoulderAttempts.AsNoTracking()
            .Where(a => a.BoulderId == boulderId && a.Completed)
            .Join(db.Users, a => a.UserId, u => u.Id, (a, u) => new { a.CompletedAt, u })
            .Where(x => x.u.SuspendedAt == null && !hidden.Contains(x.u.Id) && (!x.u.LeaderboardOptOut || x.u.Id == viewer));
        var shown = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.CompletedAt).ThenBy(x => x.u.DisplayName)
            .Skip((p - 1) * size).Take(size)
            .Select(x => new CompletionDto(new PersonDto(x.u.Id, x.u.DisplayName, x.u.AvatarUrl), x.CompletedAt))
            .ToListAsync(ct);
        return new BoulderCompletionsDto(total, new PagedResult<CompletionDto>(items, p, size, shown));
    }
}
