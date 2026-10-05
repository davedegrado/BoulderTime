using BoulderTime.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BoulderTime.Application.Users;

/// <summary>
/// Erases accounts whose week of grace has passed.
///
/// Everything personal goes: name, email, photo, attempts, sends, ratings, grade suggestions, comments, likes,
/// community videos (files included), follows, notifications, registered devices, and the Supabase sign-in itself.
/// What stays belongs to the gym rather than to the person — the boulders they set, the official beta they filmed —
/// so the row survives as a nameless placeholder instead of taking the gym's own content down with it.
/// </summary>
public sealed class AccountEraser(IAppDbContext db, IObjectStorage storage, IAuthAdmin auth, IClock clock, ILogger<AccountEraser> logger)
{
    public async Task<int> EraseDueAsync(CancellationToken ct = default)
    {
        var cutoff = clock.UtcNow - AccountDeletionService.GracePeriod;
        var due = await db.Users.Where(u => u.DeletionRequestedAt != null && u.DeletionRequestedAt <= cutoff).ToListAsync(ct);
        foreach (var user in due) await EraseAsync(user.Id, ct);
        return due.Count;
    }

    private async Task TryDeleteAsync(string bucket, string path, CancellationToken ct)
    {
        // A file that refuses to go must not stop the rest of the erasure: the database is what the app reads.
        try { await storage.DeleteAsync(bucket, path, ct); }
        catch (Exception e) { logger.LogWarning(e, "Could not delete a stored file while erasing an account"); }
    }

    public async Task EraseAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId, ct);
        if (user is null) return;

        // Files first: a row removed without its file would leave the video reachable by its signed URL.
        var videos = await db.BoulderVideos.Where(v => v.UserId == userId).ToListAsync(ct);
        foreach (var video in videos)
        {
            await TryDeleteAsync(StorageBuckets.CommunityVideos, video.StoragePath, ct);
            if (video.ThumbnailPath is { Length: > 0 } thumbnail) await TryDeleteAsync(StorageBuckets.CommunityVideos, thumbnail, ct);
        }
        if (user.AvatarPath is { Length: > 0 } avatar) await TryDeleteAsync(StorageBuckets.Avatars, avatar, ct);

        db.BoulderVideos.RemoveRange(videos);
        db.CommentLikes.RemoveRange(await db.CommentLikes.Where(x => x.UserId == userId).ToListAsync(ct));
        db.Comments.RemoveRange(await db.Comments.Where(x => x.UserId == userId).ToListAsync(ct));
        db.GradeSuggestions.RemoveRange(await db.GradeSuggestions.Where(x => x.UserId == userId).ToListAsync(ct));
        db.BoulderRatings.RemoveRange(await db.BoulderRatings.Where(x => x.UserId == userId).ToListAsync(ct));
        db.BoulderAttempts.RemoveRange(await db.BoulderAttempts.Where(x => x.UserId == userId).ToListAsync(ct));
        db.GymFollows.RemoveRange(await db.GymFollows.Where(x => x.UserId == userId).ToListAsync(ct));
        db.SectorFollows.RemoveRange(await db.SectorFollows.Where(x => x.UserId == userId).ToListAsync(ct));
        db.BoulderFollows.RemoveRange(await db.BoulderFollows.Where(x => x.UserId == userId).ToListAsync(ct));
        db.Notifications.RemoveRange(await db.Notifications.Where(x => x.UserId == userId).ToListAsync(ct));
        db.PushSubscriptions.RemoveRange(await db.PushSubscriptions.Where(x => x.UserId == userId).ToListAsync(ct));
        db.GymStaff.RemoveRange(await db.GymStaff.Where(x => x.UserId == userId).ToListAsync(ct));
        db.Reports.RemoveRange(await db.Reports.Where(x => x.ReportedByUserId == userId).ToListAsync(ct));
        db.LeaderboardReports.RemoveRange(await db.LeaderboardReports.Where(x => x.ReportedUserId == userId || x.ReportedByUserId == userId).ToListAsync(ct));

        user.Anonymise(clock.UtcNow);
        await db.SaveChangesAsync(ct);

        // Last: without the sign-in, the person can no longer reach anything, whatever else went wrong above.
        await auth.DeleteUserAsync(userId, ct);
        logger.LogInformation("Erased an account after its grace period");
    }
}
