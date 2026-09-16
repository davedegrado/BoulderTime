using BoulderTime.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoulderTime.Infrastructure.Persistence.Configurations;

internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> b)
    {
        b.ToTable("users");
        b.HasKey(u => u.Id);
        // The id comes from Supabase Auth; never generate it in the database.
        b.Property(u => u.Id).ValueGeneratedNever();

        b.Property(u => u.Email).HasMaxLength(User.EmailMaxLength).IsRequired();
        // Non-unique: a deleted-then-recreated auth account gets a new id with the same email.
        // Used by staff invitations (Phase 2) to match invitees.
        b.HasIndex(u => u.Email);

        b.Property(u => u.DisplayName).HasMaxLength(User.DisplayNameMaxLength).IsRequired();
        b.Property(u => u.AvatarUrl).HasMaxLength(2048);
        b.Property(u => u.IsPlatformAdmin).HasDefaultValue(false);
        b.Property(u => u.ProfileVisibility).HasConversion<string>().HasMaxLength(32).HasDefaultValue(ProfileVisibility.Public);
        b.Property(u => u.CreatedAt).IsRequired();
        b.Property(u => u.UpdatedAt).IsRequired();
    }
}
