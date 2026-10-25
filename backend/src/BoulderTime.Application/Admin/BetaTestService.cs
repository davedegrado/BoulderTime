using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Common;
using BoulderTime.Application.Gyms;
using BoulderTime.Domain.Boulders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BoulderTime.Application.Admin;

/// <summary>The gym's slug, typed again: the call only goes ahead on the gym it names.</summary>
public sealed record BetaTestRequest(string? GymSlug);

/// <param name="BetaTestBoulders">The gym's boulders now marked as trial ones.</param>
/// <param name="Changed">How many this call marked (or, undoing, unmarked).</param>
/// <param name="BetaTesters">People who got the Beta Tester badge with this call (ADR-069).</param>
public sealed record BetaTestResultDto(int BetaTestBoulders, int Changed, int BetaTesters = 0);

/// <summary>
/// Closes a gym's trial period when it launches BoulderTime for real. Every boulder off the wall (removed, or deleted
/// since) becomes a trial boulder: it stays in the history of the climbers who tested the app, with "Betatest" next to
/// its sector, but scores no points and counts in no leaderboard. The boulders still on the wall are the gym's real
/// ones and are left alone.
///
/// Run it on launch day. Running it again marks the boulders taken down since — the ones staff forgot — and leaves the
/// others as they are; after launch it would also mark the gym's ordinary retraces, so it is not for later use.
/// Platform administrators only.
///
/// It is also the gym's launch (ADR-069): the first run sets its launch date, from which its leaderboards hand out titles,
/// and whoever climbed any of its boulders so far — on the wall or not — gets the Beta Tester badge. Undoing it takes
/// the launch back, not the badges.
/// </summary>
public sealed class BetaTestService(IAppDbContext db, GymAccess access, ICurrentUser currentUser, IClock clock, ILogger<BetaTestService> logger,
    Badges.BadgeAwarder badges)
{
    public async Task<BetaTestResultDto> MarkAsync(Guid gymId, BetaTestRequest r, CancellationToken ct = default)
    {
        await RequireConfirmedAsync(gymId, r, ct);
        var now = clock.UtcNow;
        var boulders = await db.Boulders.Where(b => b.GymId == gymId && b.Status != BoulderStatus.Active).ToListAsync(ct);
        var changed = boulders.Count(b => b.MarkBetaTest(now));
        var gym = await db.Gyms.FirstAsync(g => g.Id == gymId, ct);
        gym.Launch(now);
        await db.SaveChangesAsync(ct);
        var total = boulders.Count(b => b.BetaTestAt is not null);
        logger.LogInformation("Platform admin {UserId} marked {Changed} boulders of gym {GymId} as trial ones ({Total} in all)",
            currentUser.UserId, changed, gymId, total);

        // After the marking is saved: the badges must never be the reason it isn't.
        var testers = 0;
        try { testers = await badges.AwardBetaTestersAsync(gymId, ct); }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogError(e, "Could not award the Beta Tester badges of gym {GymId}; running the beta test again awards them", gymId);
        }
        return new BetaTestResultDto(total, changed, testers);
    }

    /// <summary>Takes the mark off every boulder of the gym, in case it was put on by mistake.</summary>
    public async Task<BetaTestResultDto> UnmarkAsync(Guid gymId, BetaTestRequest r, CancellationToken ct = default)
    {
        await RequireConfirmedAsync(gymId, r, ct);
        var boulders = await db.Boulders.Where(b => b.GymId == gymId && b.BetaTestAt != null).ToListAsync(ct);
        var changed = boulders.Count(b => b.UnmarkBetaTest());
        (await db.Gyms.FirstAsync(g => g.Id == gymId, ct)).Unlaunch();
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Platform admin {UserId} unmarked {Changed} trial boulders of gym {GymId}", currentUser.UserId, changed, gymId);
        return new BetaTestResultDto(0, changed);
    }

    /// <summary>A platform administrator, and the gym's slug typed again, so it can't land on the wrong gym by a pasted id.</summary>
    private async Task RequireConfirmedAsync(Guid gymId, BetaTestRequest r, CancellationToken ct)
    {
        await access.RequirePlatformAdminAsync(ct);
        var gym = await db.Gyms.AsNoTracking().FirstOrDefaultAsync(g => g.Id == gymId, ct) ?? throw new NotFoundException("Gym", gymId);
        if (!string.Equals(Input.Trimmed(r.GymSlug), gym.Slug, StringComparison.Ordinal))
            throw new ValidationException("gymSlug", "Type the gym's slug to confirm.");
    }
}
