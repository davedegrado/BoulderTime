using BoulderTime.Domain.Candidates;
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

    /// <exception cref="Common.UniqueConstraintViolationException">A unique index was violated.</exception>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
