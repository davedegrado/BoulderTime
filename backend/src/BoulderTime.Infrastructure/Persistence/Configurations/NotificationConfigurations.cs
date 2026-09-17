using BoulderTime.Domain.Gyms;
using BoulderTime.Domain.Notifications;
using BoulderTime.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoulderTime.Infrastructure.Persistence.Configurations;

internal sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> b)
    {
        b.ToTable("notifications");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(32).IsRequired();
        b.Property(x => x.RelatedEntityType).HasConversion<string>().HasMaxLength(16).IsRequired();
        b.Property(x => x.Title).HasMaxLength(Notification.TitleMaxLength).IsRequired();
        b.Property(x => x.Body).HasMaxLength(Notification.BodyMaxLength);
        b.Property(x => x.Link).HasMaxLength(512).IsRequired();
        b.Property(x => x.CollapseKey).HasMaxLength(100);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => new { x.UserId, x.CreatedAt });                       // inbox
        b.HasIndex(x => new { x.UserId, x.ReadAt });                          // unread badge
        b.HasIndex(x => new { x.CollapseKey, x.UserId }).HasFilter("read_at IS NULL AND collapse_key IS NOT NULL");
    }
}

internal sealed class NotificationSettingsConfiguration : IEntityTypeConfiguration<NotificationSettings>
{
    public void Configure(EntityTypeBuilder<NotificationSettings> b)
    {
        b.ToTable("notification_settings");
        b.HasKey(x => x.UserId);
        b.Property(x => x.UserId).ValueGeneratedNever();
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class GymAnnouncementConfiguration : IEntityTypeConfiguration<GymAnnouncement>
{
    public void Configure(EntityTypeBuilder<GymAnnouncement> b)
    {
        b.ToTable("gym_announcements");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(24).IsRequired();
        b.Property(x => x.Title).HasMaxLength(GymAnnouncement.TitleMaxLength).IsRequired();
        b.Property(x => x.Content).HasMaxLength(GymAnnouncement.ContentMaxLength).IsRequired();
        b.Property(x => x.ImagePath).HasMaxLength(512);
        b.HasOne<Gym>().WithMany().HasForeignKey(x => x.GymId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<Sector>().WithMany().HasForeignKey(x => x.SectorId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.GymId, x.CreatedAt });
    }
}
