using BoulderTime.Domain.Boulders;
using BoulderTime.Domain.Candidates;
using BoulderTime.Domain.Climbing;
using BoulderTime.Domain.Community;
using BoulderTime.Domain.Follows;
using BoulderTime.Domain.Notifications;
using BoulderTime.Domain.Grading;
using BoulderTime.Domain.Gyms;
using BoulderTime.Domain.Leaderboards;
using BoulderTime.Domain.Staff;
using BoulderTime.Domain.Users;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Abstractions;

/// <summary>
/// Application-facing view of the database. Use-case services depend on this rather than on the
/// concrete Npgsql DbContext, so business logic stays testable and provider-agnostic.
/// </summary>
public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<Gym> Gyms { get; }
    DbSet<Sector> Sectors { get; }
    DbSet<GymStaffMember> GymStaff { get; }
    DbSet<StaffInvitation> StaffInvitations { get; }
    DbSet<GymCandidate> GymCandidates { get; }
    DbSet<GradeSystem> GradeSystems { get; }
    DbSet<GradeValue> GradeValues { get; }
    DbSet<Boulder> Boulders { get; }
    DbSet<IntensityVote> IntensityVotes { get; }
    DbSet<BoulderTime.Domain.Apps.AppVersionPolicy> AppVersionPolicies { get; }
    DbSet<PhotoRecompression> PhotoRecompressions { get; }
    DbSet<BoulderGrade> BoulderGrades { get; }
    DbSet<GymTag> GymTags { get; }
    DbSet<BoulderTag> BoulderTags { get; }
    DbSet<BoulderAttempt> BoulderAttempts { get; }
    DbSet<BoulderRating> BoulderRatings { get; }
    DbSet<GymFollow> GymFollows { get; }
    DbSet<SectorFollow> SectorFollows { get; }
    DbSet<BoulderFollow> BoulderFollows { get; }
    DbSet<Comment> Comments { get; }
    DbSet<CommentLike> CommentLikes { get; }
    DbSet<GradeSuggestion> GradeSuggestions { get; }
    DbSet<BoulderBeta> BoulderBetas { get; }
    DbSet<BoulderVideo> BoulderVideos { get; }
    DbSet<Report> Reports { get; }
    DbSet<Notification> Notifications { get; }
    DbSet<NotificationSettings> NotificationSettings { get; }
    DbSet<GymAnnouncement> GymAnnouncements { get; }
    DbSet<AnnouncementAttendance> AnnouncementAttendances { get; }
    DbSet<AnnouncementDismissal> AnnouncementDismissals { get; }
    DbSet<EarlyPartnership> EarlyPartnerships { get; }
    DbSet<LeaderboardReport> LeaderboardReports { get; }
    DbSet<PushSubscription> PushSubscriptions { get; }
    DbSet<UserBlock> UserBlocks { get; }
    DbSet<Friendship> Friendships { get; }
    DbSet<UserReport> UserReports { get; }
    DbSet<SuspendedEmail> SuspendedEmails { get; }

    /// <exception cref="Common.UniqueConstraintViolationException">A unique index was violated.</exception>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
