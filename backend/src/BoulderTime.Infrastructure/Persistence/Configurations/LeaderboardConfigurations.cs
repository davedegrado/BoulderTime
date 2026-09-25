using BoulderTime.Domain.Gyms;
using BoulderTime.Domain.Leaderboards;
using BoulderTime.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoulderTime.Infrastructure.Persistence.Configurations;

internal sealed class LeaderboardReportConfiguration : IEntityTypeConfiguration<LeaderboardReport>
{
    public void Configure(EntityTypeBuilder<LeaderboardReport> b)
    {
        b.ToTable("leaderboard_reports");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Reason).HasMaxLength(LeaderboardReport.ReasonMaxLength).IsRequired();
        b.Property(x => x.HandlingNote).HasMaxLength(LeaderboardReport.ReasonMaxLength);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.HasOne<Gym>().WithMany().HasForeignKey(x => x.GymId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ReportedUserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ReportedByUserId).OnDelete(DeleteBehavior.Restrict);
        // One open report per gym and climber: reporting twice updates nothing instead of filling the queue.
        b.HasIndex(x => new { x.GymId, x.ReportedUserId }).IsUnique().HasFilter("status = 'Pending'");
        b.HasIndex(x => x.Status);
    }
}
