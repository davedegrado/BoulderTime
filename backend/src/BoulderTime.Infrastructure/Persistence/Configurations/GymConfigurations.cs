using BoulderTime.Domain.Candidates;
using BoulderTime.Domain.Common;
using BoulderTime.Domain.Gyms;
using BoulderTime.Domain.Staff;
using BoulderTime.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoulderTime.Infrastructure.Persistence.Configurations;

internal sealed class GymConfiguration : IEntityTypeConfiguration<Gym>
{
    public void Configure(EntityTypeBuilder<Gym> b)
    {
        b.ToTable("gyms");
        b.HasKey(g => g.Id);
        b.Property(g => g.Id).ValueGeneratedNever();
        b.Property(g => g.Name).HasMaxLength(Gym.NameMaxLength).IsRequired();
        b.Property(g => g.Slug).HasMaxLength(Slug.MaxLength + 8).IsRequired();
        b.HasIndex(g => g.Slug).IsUnique();
        b.Property(g => g.Description).HasMaxLength(Gym.DescriptionMaxLength);
        b.Property(g => g.Address).HasMaxLength(Gym.AddressMaxLength);
        b.Property(g => g.City).HasMaxLength(Gym.CityMaxLength).IsRequired();
        b.Property(g => g.Website).HasMaxLength(Gym.UrlMaxLength);
        b.Property(g => g.Email).HasMaxLength(Gym.EmailMaxLength);
        b.Property(g => g.Phone).HasMaxLength(Gym.PhoneMaxLength);
        b.Property(g => g.LogoUrl).HasMaxLength(Gym.UrlMaxLength);
        b.Property(g => g.CoverImageUrl).HasMaxLength(Gym.UrlMaxLength);
        b.Property(g => g.LogoPath).HasMaxLength(512);
        b.Property(g => g.CoverImagePath).HasMaxLength(512);
        b.Property(g => g.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        b.HasIndex(g => new { g.Status, g.Name });
        b.HasIndex(g => new { g.Status, g.City });
    }
}

internal sealed class SectorConfiguration : IEntityTypeConfiguration<Sector>
{
    public void Configure(EntityTypeBuilder<Sector> b)
    {
        b.ToTable("sectors");
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).ValueGeneratedNever();
        b.Property(s => s.Name).HasMaxLength(Sector.NameMaxLength).IsRequired();
        b.Property(s => s.Description).HasMaxLength(Sector.DescriptionMaxLength);
        b.Property(s => s.ImageUrl).HasMaxLength(Gym.UrlMaxLength);
        b.HasOne<Gym>().WithMany().HasForeignKey(s => s.GymId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(s => new { s.GymId, s.Name }).IsUnique();
        b.HasIndex(s => new { s.GymId, s.SortOrder });
    }
}

internal sealed class GymStaffMemberConfiguration : IEntityTypeConfiguration<GymStaffMember>
{
    public void Configure(EntityTypeBuilder<GymStaffMember> b)
    {
        b.ToTable("gym_staff");
        b.HasKey(s => s.Id);
        b.Property(s => s.Id).ValueGeneratedNever();
        b.Property(s => s.Role).HasConversion<string>().HasMaxLength(16).IsRequired();
        b.HasOne<Gym>().WithMany().HasForeignKey(s => s.GymId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(s => s.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(s => s.InvitedByUserId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(s => new { s.GymId, s.UserId }).IsUnique();
        b.HasIndex(s => s.UserId);
    }
}

internal sealed class StaffInvitationConfiguration : IEntityTypeConfiguration<StaffInvitation>
{
    public void Configure(EntityTypeBuilder<StaffInvitation> b)
    {
        b.ToTable("staff_invitations");
        b.HasKey(i => i.Id);
        b.Property(i => i.Id).ValueGeneratedNever();
        b.Property(i => i.Email).HasMaxLength(User.EmailMaxLength).IsRequired();
        b.Property(i => i.Role).HasConversion<string>().HasMaxLength(16).IsRequired();
        b.Property(i => i.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        b.HasOne<Gym>().WithMany().HasForeignKey(i => i.GymId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(i => i.InvitedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(i => i.AcceptedByUserId).OnDelete(DeleteBehavior.SetNull);
        // At most one open invitation per (gym, email). Column names are snake_case in raw SQL.
        b.HasIndex(i => new { i.GymId, i.Email }).IsUnique().HasFilter("status = 'Pending'");
        b.HasIndex(i => new { i.Email, i.Status });
    }
}

internal sealed class GymCandidateConfiguration : IEntityTypeConfiguration<GymCandidate>
{
    public void Configure(EntityTypeBuilder<GymCandidate> b)
    {
        b.ToTable("gym_candidates");
        b.HasKey(c => c.Id);
        b.Property(c => c.Id).ValueGeneratedNever();
        b.Property(c => c.GymName).HasMaxLength(GymCandidate.NameMaxLength).IsRequired();
        b.Property(c => c.City).HasMaxLength(Gym.CityMaxLength).IsRequired();
        b.Property(c => c.OfficialEmail).HasMaxLength(User.EmailMaxLength);
        b.Property(c => c.Website).HasMaxLength(Gym.UrlMaxLength);
        b.Property(c => c.Notes).HasMaxLength(GymCandidate.NotesMaxLength);
        b.Property(c => c.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        b.HasOne<User>().WithMany().HasForeignKey(c => c.SubmittedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(c => c.HandledByUserId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<Gym>().WithMany().HasForeignKey(c => c.GymId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(c => new { c.Status, c.CreatedAt });
        b.HasIndex(c => c.SubmittedByUserId);
    }
}
