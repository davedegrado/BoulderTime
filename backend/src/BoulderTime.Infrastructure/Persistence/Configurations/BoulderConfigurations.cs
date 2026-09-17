using BoulderTime.Domain.Boulders;
using BoulderTime.Domain.Grading;
using BoulderTime.Domain.Gyms;
using BoulderTime.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoulderTime.Infrastructure.Persistence.Configurations;

internal sealed class GradeSystemConfiguration : IEntityTypeConfiguration<GradeSystem>
{
    public void Configure(EntityTypeBuilder<GradeSystem> b)
    {
        b.ToTable("grade_systems");
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).ValueGeneratedNever();
        b.Property(s => s.Name).HasMaxLength(GradeSystem.NameMaxLength).IsRequired();
        b.Property(s => s.Type).HasConversion<string>().HasMaxLength(24).IsRequired();
        b.HasOne<Gym>().WithMany().HasForeignKey(s => s.GymId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(s => new { s.GymId, s.Name }).IsUnique();
        b.HasIndex(s => new { s.GymId, s.SortOrder });
    }
}

internal sealed class GradeValueConfiguration : IEntityTypeConfiguration<GradeValue>
{
    public void Configure(EntityTypeBuilder<GradeValue> b)
    {
        b.ToTable("grade_values");
        b.HasKey(v => v.Id);
        b.Property(v => v.Id).ValueGeneratedNever();
        b.Property(v => v.Label).HasMaxLength(GradeValue.LabelMaxLength).IsRequired();
        b.Property(v => v.ColorHex).HasMaxLength(7);
        b.HasOne<GradeSystem>().WithMany().HasForeignKey(v => v.GradeSystemId).OnDelete(DeleteBehavior.Restrict);
        // Label uniqueness among ACTIVE values is enforced by the service; retired values may repeat a label.
        b.HasIndex(v => new { v.GradeSystemId, v.Rank });
    }
}

internal sealed class BoulderConfiguration : IEntityTypeConfiguration<Boulder>
{
    public void Configure(EntityTypeBuilder<Boulder> b)
    {
        b.ToTable("boulders");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.PhotoPath).HasMaxLength(512).IsRequired();
        b.Property(x => x.ThumbnailPath).HasMaxLength(512);
        b.Property(x => x.HoldColor).HasConversion<string>().HasMaxLength(16).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        // Restrict everywhere: boulders are never deleted, and nothing may cascade into climbing history.
        b.HasOne<Gym>().WithMany().HasForeignKey(x => x.GymId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Sector>().WithMany().HasForeignKey(x => x.SectorId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.SetterUserId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.RemovedByUserId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => new { x.GymId, x.Status, x.CreatedAt });
        b.HasIndex(x => new { x.SectorId, x.Status });
    }
}

internal sealed class BoulderGradeConfiguration : IEntityTypeConfiguration<BoulderGrade>
{
    public void Configure(EntityTypeBuilder<BoulderGrade> b)
    {
        b.ToTable("boulder_grades");
        b.HasKey(g => g.Id);
        b.Property(g => g.Id).ValueGeneratedNever();
        b.Property(g => g.Source).HasConversion<string>().HasMaxLength(16).IsRequired();
        b.HasOne<Boulder>().WithMany().HasForeignKey(g => g.BoulderId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<GradeSystem>().WithMany().HasForeignKey(g => g.GradeSystemId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<GradeValue>().WithMany().HasForeignKey(g => g.GradeValueId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(g => g.CreatedByUserId).OnDelete(DeleteBehavior.SetNull);
        // One official grade per (boulder, system).
        b.HasIndex(g => new { g.BoulderId, g.GradeSystemId }).IsUnique().HasFilter("source = 'Staff'");
        b.HasIndex(g => g.GradeValueId);
    }
}
