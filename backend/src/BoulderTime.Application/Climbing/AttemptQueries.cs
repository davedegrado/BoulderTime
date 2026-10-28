using BoulderTime.Application.Abstractions;
using BoulderTime.Domain.Boulders;
using BoulderTime.Domain.Climbing;

namespace BoulderTime.Application.Climbing;

public static class AttemptQueries
{
    /// <summary>
    /// Only what was climbed on boulders that still exist: one the gym deleted (not merely took down) leaves a
    /// person's history, recent activity, numbers, highest grades, weeks, gyms and favourite sector, and their friends'
    /// view of them. The rows stay in the database; the leaderboards and the badges already earned are not changed.
    /// </summary>
    public static IQueryable<BoulderAttempt> OnExistingBoulders(this IQueryable<BoulderAttempt> attempts, IAppDbContext db) =>
        attempts.Where(a => db.Boulders.Any(b => b.Id == a.BoulderId && b.Status != BoulderStatus.Deleted));
}
