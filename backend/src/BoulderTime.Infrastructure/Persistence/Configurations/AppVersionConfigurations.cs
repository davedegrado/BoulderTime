using BoulderTime.Domain.Apps;
using BoulderTime.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoulderTime.Infrastructure.Persistence.Configurations;

internal sealed class AppVersionPolicyConfiguration : IEntityTypeConfiguration<AppVersionPolicy>
{
    public void Configure(EntityTypeBuilder<AppVersionPolicy> b)
    {
        b.ToTable("app_version_policies");
        b.HasKey(x => x.Platform);
        b.Property(x => x.Platform).HasConversion<string>().HasMaxLength(16);
        b.Property(x => x.MinimumVersion).HasMaxLength(AppVersionPolicy.VersionMaxLength).IsRequired();
        b.Property(x => x.RecommendedVersion).HasMaxLength(AppVersionPolicy.VersionMaxLength);
        b.Property(x => x.StoreUrl).HasMaxLength(AppVersionPolicy.StoreUrlMaxLength);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UpdatedByUserId).OnDelete(DeleteBehavior.SetNull);
        // The versions on the stores when this was introduced: nobody is asked to update until an admin says so.
        var since = new DateTimeOffset(2026, 10, 22, 0, 0, 0, TimeSpan.Zero);
        b.HasData(
            new { Platform = AppPlatform.Ios, MinimumVersion = "1.0.0", RecommendedVersion = (string?)null, StoreUrl = (string?)null, UpdatedAt = since, UpdatedByUserId = (Guid?)null },
            new { Platform = AppPlatform.Android, MinimumVersion = "1.0.0", RecommendedVersion = (string?)null,
                StoreUrl = (string?)"https://play.google.com/store/apps/details?id=com.bouldertime.app", UpdatedAt = since, UpdatedByUserId = (Guid?)null });
    }
}
