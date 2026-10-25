using BoulderTime.Application.Abstractions;
using BoulderTime.Domain.Community;
using BoulderTime.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BoulderTime.Application.Users;

/// <summary>
/// Erases accounts whose week of grace has passed.
///
/// Everything personal goes: name, email, photo, attempts, sends, ratings, grade suggestions, comments, likes,
/// community videos (files included), follows, badges and active days, the events they said they'd come to, notifications and their settings, registered devices, reports and
/// blocks made by or about the person (reports on their comments and videos included), staff invitations sent to
/// their address, and the Supabase sign-in itself. A suspended account leaves one thing behind: the hash of its address
/// (ADR-038), so that it can't sign up again unsuspended.
/// What stays belongs to the gym rather than to the person — the boulders they set, the official beta they filmed —
/// so the row survives as a nameless placeholder instead of taking the gym's own content down with it.
/// </summary>
public sealed class AccountEraser(IAppDbContext db, IObjectStorage storage, IAuthAdmin auth, IClock clock, ILogger<AccountEraser> logger)
{
    public async Task<int> EraseDueAsync(CancellationToken ct = default)
    {
        var cutoff = clock.UtcNow - AccountDeletionService.GracePeriod;
        // Already-erased rows keep a DeletionRequestedAt too; without the address check they would be erased again on
        // every run, for ever.
        var due = await db.Users.Where(u => u.DeletionRequestedAt != null && u.DeletionRequestedAt <= cutoff
                                            && !u.Email.EndsWith(User.ErasedEmailDomain)).ToListAsync(ct);
        foreach (var user in due) await EraseAsync(user.Id, ct);
        await ForgetExpiredSuspendedEmailsAsync(ct);
        return due.Count;
    }

    /// <summary>The fingerprints of suspended addresses are kept for a set time, not for ever (ADR-038).</summary>
    public async Task ForgetExpiredSuspendedEmailsAsync(CancellationToken ct = default)
    {
        var expiredBefore = clock.UtcNow - SuspendedEmail.RetentionPeriod;
        var expired = await db.SuspendedEmails.Where(x => x.RecordedAt <= expiredBefore).ToListAsync(ct);
        if (expired.Count == 0) return;
        db.SuspendedEmails.RemoveRange(expired);
        await db.SaveChangesAsync(ct);
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
        if (user is null || user.Email.EndsWith(User.ErasedEmailDomain, StringComparison.Ordinal)) return;
        var email = user.Email.Trim().ToLowerInvariant();

        // Files first: a row removed without its file would leave the video reachable by its signed URL.
        var videos = await db.BoulderVideos.Where(v => v.UserId == userId).ToListAsync(ct);
        foreach (var video in videos)
        {
            await TryDeleteAsync(StorageBuckets.CommunityVideos, video.StoragePath, ct);
            if (video.ThumbnailPath is { Length: > 0 } thumbnail) await TryDeleteAsync(StorageBuckets.CommunityVideos, thumbnail, ct);
        }
        if (user.AvatarPath is { Length: > 0 } avatar) await TryDeleteAsync(StorageBuckets.Avatars, avatar, ct);

        // Reports about their comments and videos describe the person: they go with the content.
        var commentIds = await db.Comments.Where(x => x.UserId == userId).Select(x => x.Id).ToListAsync(ct);
        var videoIds = videos.Select(v => v.Id).ToList();
        db.Reports.RemoveRange(await db.Reports.Where(x =>
            (x.EntityType == ReportEntityType.Comment && commentIds.Contains(x.EntityId)) ||
            (x.EntityType == ReportEntityType.Video && videoIds.Contains(x.EntityId))).ToListAsync(ct));

        db.BoulderVideos.RemoveRange(videos);
        db.CommentLikes.RemoveRange(await db.CommentLikes.Where(x => x.UserId == userId).ToListAsync(ct));
        db.Comments.RemoveRange(await db.Comments.Where(x => x.UserId == userId).ToListAsync(ct));
        db.GradeSuggestions.RemoveRange(await db.GradeSuggestions.Where(x => x.UserId == userId).ToListAsync(ct));
        db.IntensityVotes.RemoveRange(await db.IntensityVotes.Where(x => x.UserId == userId).ToListAsync(ct));
        db.BoulderRatings.RemoveRange(await db.BoulderRatings.Where(x => x.UserId == userId).ToListAsync(ct));
        db.BoulderAttempts.RemoveRange(await db.BoulderAttempts.Where(x => x.UserId == userId).ToListAsync(ct));
        db.BoulderProjects.RemoveRange(await db.BoulderProjects.Where(x => x.UserId == userId).ToListAsync(ct));
        db.GymFollows.RemoveRange(await db.GymFollows.Where(x => x.UserId == userId).ToListAsync(ct));
        db.AnnouncementAttendances.RemoveRange(await db.AnnouncementAttendances.Where(x => x.UserId == userId).ToListAsync(ct));
        db.AnnouncementDismissals.RemoveRange(await db.AnnouncementDismissals.Where(x => x.UserId == userId).ToListAsync(ct));
        db.SectorFollows.RemoveRange(await db.SectorFollows.Where(x => x.UserId == userId).ToListAsync(ct));
        db.BoulderFollows.RemoveRange(await db.BoulderFollows.Where(x => x.UserId == userId).ToListAsync(ct));
        db.Notifications.RemoveRange(await db.Notifications.Where(x => x.UserId == userId).ToListAsync(ct));
        db.NotificationSettings.RemoveRange(await db.NotificationSettings.Where(x => x.UserId == userId).ToListAsync(ct));
        db.PushSubscriptions.RemoveRange(await db.PushSubscriptions.Where(x => x.UserId == userId).ToListAsync(ct));
        db.StaffInvitations.RemoveRange(await db.StaffInvitations.Where(x => x.Email == email).ToListAsync(ct));
        db.GymStaff.RemoveRange(await db.GymStaff.Where(x => x.UserId == userId).ToListAsync(ct));
        db.Reports.RemoveRange(await db.Reports.Where(x => x.ReportedByUserId == userId).ToListAsync(ct));
        db.LeaderboardReports.RemoveRange(await db.LeaderboardReports.Where(x => x.ReportedUserId == userId || x.ReportedByUserId == userId).ToListAsync(ct));
        db.UserReports.RemoveRange(await db.UserReports.Where(x => x.ReportedUserId == userId || x.ReportedByUserId == userId).ToListAsync(ct));
        db.UserBlocks.RemoveRange(await db.UserBlocks.Where(x => x.BlockerUserId == userId || x.BlockedUserId == userId).ToListAsync(ct));
        db.Friendships.RemoveRange(await db.Friendships.Where(x => x.UserLowId == userId || x.UserHighId == userId).ToListAsync(ct));
        db.BadgeAwards.RemoveRange(await db.BadgeAwards.Where(x => x.UserId == userId).ToListAsync(ct));
        db.ActivityDays.RemoveRange(await db.ActivityDays.Where(x => x.UserId == userId).ToListAsync(ct));

        // A suspended account leaving keeps only the fingerprint of its address, so it can't come back clean (ADR-038).
        if (user.IsSuspended)
        {
            var hash = SuspendedEmail.Hash(user.Email);
            var known = await db.SuspendedEmails.FirstOrDefaultAsync(x => x.EmailHash == hash, ct);
            if (known is null) db.SuspendedEmails.Add(SuspendedEmail.Record(user.Email, clock.UtcNow));
            else known.Renew(clock.UtcNow);
        }

        user.Anonymise(clock.UtcNow);
        await db.SaveChangesAsync(ct);

        // Last: without the sign-in, the person can no longer reach anything, whatever else went wrong above.
        await auth.DeleteUserAsync(userId, ct);
        logger.LogInformation("Erased an account after its grace period");
    }
}
