using BoulderTime.Domain.Shoes;
using BoulderTime.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoulderTime.Infrastructure.Persistence.Configurations;

internal sealed class ClimbingShoeConfiguration : IEntityTypeConfiguration<ClimbingShoe>
{
    public void Configure(EntityTypeBuilder<ClimbingShoe> b)
    {
        b.ToTable("climbing_shoes");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Brand).HasMaxLength(60).IsRequired();
        b.Property(x => x.Name).HasMaxLength(100).IsRequired();
        b.Property(x => x.Key).HasMaxLength(170).IsRequired();
        b.Property(x => x.SearchText).HasMaxLength(170).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.ImageUrl).HasMaxLength(500);
        b.Property(x => x.ProductUrl).HasMaxLength(500);
        b.Property(x => x.SourceUrl).HasMaxLength(500);
        // One model once, whatever its case, accents or spaces.
        b.HasIndex(x => x.Key).IsUnique();
        b.HasIndex(x => new { x.Brand, x.Name });
    }
}

internal sealed class UserClimbingShoeConfiguration : IEntityTypeConfiguration<UserClimbingShoe>
{
    public void Configure(EntityTypeBuilder<UserClimbingShoe> b)
    {
        b.ToTable("user_climbing_shoes");
        // A model once per person.
        b.HasKey(x => new { x.UserId, x.ShoeId });
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        // A model people have can't be deleted from the catalogue.
        b.HasOne<ClimbingShoe>().WithMany().HasForeignKey(x => x.ShoeId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.ShoeId);
        // At most one main pair per person, kept by the database itself.
        b.HasIndex(x => x.UserId).IsUnique().HasFilter("is_primary").HasDatabaseName("ux_user_climbing_shoes_one_primary");
    }
}
