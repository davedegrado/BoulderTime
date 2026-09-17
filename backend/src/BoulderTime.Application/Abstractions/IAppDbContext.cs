using BoulderTime.Domain.Boulders;
using BoulderTime.Domain.Candidates;
using BoulderTime.Domain.Climbing;
using BoulderTime.Domain.Follows;
using BoulderTime.Domain.Grading;
using BoulderTime.Domain.Gyms;
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
    DbSet<BoulderGrade> BoulderGrades { get; }
    DbSet<BoulderAttempt> BoulderAttempts { get; }
    DbSet<BoulderRating> BoulderRatings { get; }
    DbSet<GymFollow> GymFollows { get; }
    DbSet<SectorFollow> SectorFollows { get; }
    DbSet<BoulderFollow> BoulderFollows { get; }

    /// <exception cref="Common.UniqueConstraintViolationException">A unique index was violated.</exception>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
