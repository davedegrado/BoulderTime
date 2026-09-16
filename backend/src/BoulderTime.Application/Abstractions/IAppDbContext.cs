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

    /// <exception cref="Common.UniqueConstraintViolationException">A unique index was violated.</exception>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
