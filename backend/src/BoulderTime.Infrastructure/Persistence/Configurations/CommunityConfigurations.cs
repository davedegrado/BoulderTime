using BoulderTime.Domain.Boulders;
using BoulderTime.Domain.Community;
using BoulderTime.Domain.Grading;
using BoulderTime.Domain.Gyms;
using BoulderTime.Domain.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace BoulderTime.Infrastructure.Persistence.Configurations;

internal sealed class CommentConfiguration : IEntityTypeConfiguration<Comment>
{
    public void Configure(EntityTypeBuilder<Comment> b)
    {
        b.ToTable("comments");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.Content).HasMaxLength(Comment.MaxLength).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        b.HasOne<Boulder>().WithMany().HasForeignKey(x => x.BoulderId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => new { x.BoulderId, x.CreatedAt });
    }
}

internal sealed class CommentLikeConfiguration : IEntityTypeConfiguration<CommentLike>
{
    public void Configure(EntityTypeBuilder<CommentLike> b)
    {
        b.ToTable("comment_likes");
        b.HasKey(x => new { x.CommentId, x.UserId }); // UNIQUE(comment_id, user_id)
        b.HasOne<Comment>().WithMany().HasForeignKey(x => x.CommentId).OnDelete(DeleteBehavior.Cascade);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(x => x.UserId);
    }
}

internal sealed class GradeSuggestionConfiguration : IEntityTypeConfiguration<GradeSuggestion>
{
    public void Configure(EntityTypeBuilder<GradeSuggestion> b)
    {
        b.ToTable("grade_suggestions");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.HasOne<Boulder>().WithMany().HasForeignKey(x => x.BoulderId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<GradeSystem>().WithMany().HasForeignKey(x => x.GradeSystemId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<GradeValue>().WithMany().HasForeignKey(x => x.GradeValueId).OnDelete(DeleteBehavior.Restrict);
        // One suggestion per user, per grading system, per boulder.
        b.HasIndex(x => new { x.BoulderId, x.UserId, x.GradeSystemId }).IsUnique();
        b.HasIndex(x => new { x.BoulderId, x.GradeValueId });
    }
}

internal sealed class BoulderBetaConfiguration : IEntityTypeConfiguration<BoulderBeta>
{
    public void Configure(EntityTypeBuilder<BoulderBeta> b)
    {
        b.ToTable("boulder_betas");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.StoragePath).HasMaxLength(512).IsRequired();
        b.Property(x => x.ThumbnailPath).HasMaxLength(512);
        b.Property(x => x.Caption).HasMaxLength(BoulderBeta.CaptionMaxLength);
        b.HasOne<Boulder>().WithMany().HasForeignKey(x => x.BoulderId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasIndex(x => x.BoulderId).IsUnique(); // one official beta per boulder
    }
}

internal sealed class BoulderVideoConfiguration : IEntityTypeConfiguration<BoulderVideo>
{
    public void Configure(EntityTypeBuilder<BoulderVideo> b)
    {
        b.ToTable("boulder_videos");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.StoragePath).HasMaxLength(512).IsRequired();
        b.Property(x => x.ThumbnailPath).HasMaxLength(512);
        b.Property(x => x.Caption).HasMaxLength(BoulderVideo.CaptionMaxLength);
        b.Property(x => x.RejectionReason).HasMaxLength(BoulderVideo.ReasonMaxLength);
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        b.HasOne<Boulder>().WithMany().HasForeignKey(x => x.BoulderId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ReviewedByUserId).OnDelete(DeleteBehavior.SetNull);
        b.HasIndex(x => new { x.BoulderId, x.Status });
        b.HasIndex(x => new { x.Status, x.UpdatedAt }); // moderation queue
        b.HasIndex(x => x.UserId);
    }
}

internal sealed class ReportConfiguration : IEntityTypeConfiguration<Report>
{
    public void Configure(EntityTypeBuilder<Report> b)
    {
        b.ToTable("reports");
        b.HasKey(x => x.Id);
        b.Property(x => x.Id).ValueGeneratedNever();
        b.Property(x => x.EntityType).HasConversion<string>().HasMaxLength(16).IsRequired();
        b.Property(x => x.Reason).HasConversion<string>().HasMaxLength(24).IsRequired();
        b.Property(x => x.Status).HasConversion<string>().HasMaxLength(16).IsRequired();
        b.Property(x => x.Description).HasMaxLength(Report.DescriptionMaxLength);
        b.Property(x => x.ResolutionNote).HasMaxLength(500);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ReportedByUserId).OnDelete(DeleteBehavior.Restrict);
        b.HasOne<User>().WithMany().HasForeignKey(x => x.ReviewedByUserId).OnDelete(DeleteBehavior.SetNull);
        b.HasOne<Gym>().WithMany().HasForeignKey(x => x.GymId).OnDelete(DeleteBehavior.Restrict);
        // One OPEN report per reporter and item; they may report again after it's closed.
        b.HasIndex(x => new { x.ReportedByUserId, x.EntityType, x.EntityId }).IsUnique().HasFilter("status = 'Pending'");
        b.HasIndex(x => new { x.GymId, x.Status, x.CreatedAt });
        b.HasIndex(x => new { x.Status, x.CreatedAt });
    }
}
