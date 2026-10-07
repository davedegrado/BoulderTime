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
        b.Property(u => u.AvatarPath).HasMaxLength(512);
        b.Property(u => u.Language).HasMaxLength(5).IsRequired().HasDefaultValue("it");
        b.Property(u => u.IsPlatformAdmin).HasDefaultValue(false);
        b.Property(u => u.ProfileVisibility).HasConversion<string>().HasMaxLength(32).HasDefaultValue(ProfileVisibility.Public);
        b.Property(u => u.CreatedAt).IsRequired();
        b.Property(u => u.UpdatedAt).IsRequired();
    }
}


internal sealed class UserBlockConfiguration : IEntityTypeConfiguration<UserBlock>
{
    public void Configure(EntityTypeBuilder<UserBlock> b)
    {
        b.ToTable("user_blocks");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Reason).HasMaxLength(UserBlock.ReasonMaxLength);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.BlockerUserId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.BlockedUserId).OnDelete(DeleteBehavior.Cascade);
        // Blocking the same person twice is one block, not two.
        b.HasIndex(x => new { x.BlockerUserId, x.BlockedUserId }).IsUnique();
        b.HasIndex(x => x.BlockedUserId);
    }
}
