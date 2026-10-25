using BoulderTime.Domain.Badges;
using BoulderTime.Domain.Gyms;
using BoulderTime.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoulderTime.Infrastructure.Persistence.Configurations;

internal sealed class BadgeAwardConfiguration : IEntityTypeConfiguration<BadgeAward>
{
    public void Configure(EntityTypeBuilder<BadgeAward> b)
    {
        b.ToTable("badge_awards", t => t.HasCheckConstraint("ck_badge_awards_featured_order", "featured_order IS NULL OR featured_order BETWEEN 1 AND 3"));
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.BadgeId).HasMaxLength(BadgeAward.BadgeIdMaxLength).IsRequired();
        b.Property(x => x.ScopeKey).HasMaxLength(BadgeAward.ScopeKeyMaxLength).IsRequired();
        b.Property(x => x.PeriodKind).HasConversion<string>().HasMaxLength(8);
        // Erased only with the account, by the eraser, like the rest of the history.
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Gym>().WithMany().HasForeignKey(x => x.GymId).OnDelete(DeleteBehavior.Restrict);
        // Earning the same thing twice is impossible.
        b.HasIndex(x => new { x.UserId, x.BadgeId, x.ScopeKey }).IsUnique();
        b.HasIndex(x => new { x.BadgeId, x.ScopeKey });
    }
}

internal sealed class ActivityDayConfiguration : IEntityTypeConfiguration<ActivityDay>
{
    public void Configure(EntityTypeBuilder<ActivityDay> b)
    {
        b.ToTable("activity_days");
        b.HasKey(x => new { x.UserId, x.Day });
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class CompetitionPeriodConfiguration : IEntityTypeConfiguration<CompetitionPeriod>
{
    public void Configure(EntityTypeBuilder<CompetitionPeriod> b)
    {
        b.ToTable("competition_periods");
        b.Property(x => x.Kind).HasConversion<string>().HasMaxLength(8);
        b.HasKey(x => new { x.GymId, x.Kind, x.Start });
        b.HasOne<Gym>().WithMany().HasForeignKey(x => x.GymId).OnDelete(DeleteBehavior.Cascade);
    }
}
