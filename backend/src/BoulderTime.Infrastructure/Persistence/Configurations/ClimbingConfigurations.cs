using BoulderTime.Domain.Boulders;
using BoulderTime.Domain.Climbing;
using BoulderTime.Domain.Follows;
using BoulderTime.Domain.Gyms;
using BoulderTime.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoulderTime.Infrastructure.Persistence.Configurations;

internal sealed class BoulderAttemptConfiguration : IEntityTypeConfiguration<BoulderAttempt>
{
    public void Configure(EntityTypeBuilder<BoulderAttempt> b)
    {
        b.ToTable("boulder_attempts", t => t.HasCheckConstraint("ck_boulder_attempts_attempts", "attempts >= 0 AND attempts <= 999"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        // Restrict: climbing history must never disappear through a cascade.
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Boulder>().WithMany().HasForeignKey(x => x.BoulderId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.UserId, x.BoulderId }).IsUnique();
        b.HasIndex(x => new { x.UserId, x.Completed, x.CompletedAt });
        b.HasIndex(x => x.BoulderId);
    }
}

internal sealed class BoulderRatingConfiguration : IEntityTypeConfiguration<BoulderRating>
{
    public void Configure(EntityTypeBuilder<BoulderRating> b)
    {
        b.ToTable("boulder_ratings", t => t.HasCheckConstraint("ck_boulder_ratings_rating", "rating BETWEEN 1 AND 5"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Boulder>().WithMany().HasForeignKey(x => x.BoulderId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.UserId, x.BoulderId }).IsUnique();
        b.HasIndex(x => x.BoulderId);
    }
}

internal sealed class GymFollowConfiguration : IEntityTypeConfiguration<GymFollow>
{
    public void Configure(EntityTypeBuilder<GymFollow> b)
    {
        b.ToTable("gym_follows");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Gym>().WithMany().HasForeignKey(x => x.GymId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.UserId, x.GymId }).IsUnique();
        b.HasIndex(x => x.GymId); // notification targeting
    }
}

internal sealed class SectorFollowConfiguration : IEntityTypeConfiguration<SectorFollow>
{
    public void Configure(EntityTypeBuilder<SectorFollow> b)
    {
        b.ToTable("sector_follows");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Sector>().WithMany().HasForeignKey(x => x.SectorId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.UserId, x.SectorId }).IsUnique();
        b.HasIndex(x => x.SectorId);
    }
}

internal sealed class BoulderFollowConfiguration : IEntityTypeConfiguration<BoulderFollow>
{
    public void Configure(EntityTypeBuilder<BoulderFollow> b)
    {
        b.ToTable("boulder_follows");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<Boulder>().WithMany().HasForeignKey(x => x.BoulderId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.UserId, x.BoulderId }).IsUnique();
        b.HasIndex(x => x.BoulderId);
    }
}
