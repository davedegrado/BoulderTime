using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Common;
using BoulderTime.Domain.Candidates;
using BoulderTime.Domain.Common;
using BoulderTime.Domain.Gyms;
using BoulderTime.Domain.Staff;
using BoulderTime.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BoulderTime.Infrastructure.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options, IClock clock) : DbContext(options), IAppDbContext
{
    /// <summary>
    /// All application tables live in their own schema, NOT <c>public</c>. Supabase's auto-generated Data API
    /// (PostgREST) exposes <c>public</c> to anyone holding the anon key; keeping our tables out of it means
    /// the ASP.NET API is the only way to reach application data. See docs/decisions.md (ADR-003).
    /// </summary>
    public const string Schema = "bouldertime";

    public DbSet<User> Users => Set<User>();
    public DbSet<Gym> Gyms => Set<Gym>();
    public DbSet<Sector> Sectors => Set<Sector>();
    public DbSet<GymStaffMember> GymStaff => Set<GymStaffMember>();
    public DbSet<StaffInvitation> StaffInvitations => Set<StaffInvitation>();
    public DbSet<GymCandidate> GymCandidates => Set<GymCandidate>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampAuditFields();
        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg)
        {
            throw new UniqueConstraintViolationException(pg.ConstraintName, ex);
        }
    }

    private void StampAuditFields()
    {
        var now = clock.UtcNow;
        foreach (var entry in ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.UpdatedAt = now;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(e => e.CreatedAt).IsModified = false;
                entry.Entity.UpdatedAt = now;
            }
        }
    }
}
