using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Boulders;
using BoulderTime.Application.Common;
using BoulderTime.Domain.Community;
using BoulderTime.Domain.Staff;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Community;

public sealed record CommentDto(
    Guid Id, Guid BoulderId, PersonDto Author, string Content, CommentStatus Status,
    DateTimeOffset CreatedAt, DateTimeOffset? EditedAt, int Likes, bool LikedByViewer, bool IsMine, bool CanModerate);

public sealed record SaveCommentRequest(string? Content);

/// <summary>
/// Flat comments. Everyone sees visible comments; staff of the gym also see hidden ones (marked) so they can unhide.
/// Authors edit or delete their own; staff hide/unhide. Likes are one per user.
/// </summary>
public sealed class CommentService(IAppDbContext db, BoulderAccess boulders, ICurrentUser currentUser, IClock clock, Notifications.NotificationPublisher notifications)
{
    public async Task<PagedResult<CommentDto>> ListAsync(Guid boulderId, int? page, int? pageSize, CancellationToken ct = default)
    {
        var scope = await boulders.RequireVisibleAsync(boulderId, ct);
        var (p, size) = Paging.Normalize(page, pageSize, 30);
        var q = db.Comments.AsNoTracking().Where(c => c.BoulderId == boulderId);
        var viewerId = currentUser.UserId;
        if (!scope.IsStaff) q = q.Where(c => c.Status == CommentStatus.Visible || c.UserId == viewerId);

        var total = await q.CountAsync(ct);
        var rows = await q.OrderBy(c => c.CreatedAt).Skip((p - 1) * size).Take(size).ToListAsync(ct);
        return new PagedResult<CommentDto>(await ToDtosAsync(rows, scope.IsStaff, ct), p, size, total);
    }

    public async Task<CommentDto> CreateAsync(Guid boulderId, SaveCommentRequest r, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var scope = await boulders.RequireVisibleAsync(boulderId, ct);
        var content = Validate(r.Content);
        var comment = Comment.Create(boulderId, userId, content);
        db.Comments.Add(comment);
        var authorName = await db.Users.AsNoTracking().Where(u => u.Id == userId).Select(u => u.DisplayName).FirstAsync(ct);
        await notifications.CommentAsync(comment, scope.Boulder, authorName, ct);
        await db.SaveChangesAsync(ct);
        return (await ToDtosAsync([comment], scope.IsStaff, ct))[0];
    }

    public async Task<CommentDto> EditAsync(Guid commentId, SaveCommentRequest r, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var comment = await db.Comments.FirstOrDefaultAsync(c => c.Id == commentId, ct) ?? throw new NotFoundException("Comment", commentId);
        var scope = await boulders.RequireVisibleAsync(comment.BoulderId, ct);
        if (comment.UserId != userId) throw new ForbiddenException("You can only edit your own comments.", "not_author");
        comment.Edit(Validate(r.Content), clock.UtcNow);
        await db.SaveChangesAsync(ct);
        return (await ToDtosAsync([comment], scope.IsStaff, ct))[0];
    }

    public async Task DeleteAsync(Guid commentId, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var comment = await db.Comments.FirstOrDefaultAsync(c => c.Id == commentId, ct) ?? throw new NotFoundException("Comment", commentId);
        if (comment.UserId != userId) throw new ForbiddenException("You can only delete your own comments. Staff can hide comments instead.", "not_author");
        db.CommentLikes.RemoveRange(await db.CommentLikes.Where(l => l.CommentId == commentId).ToListAsync(ct));
        db.Comments.Remove(comment);
        await db.SaveChangesAsync(ct);
    }

    public async Task<CommentDto> SetHiddenAsync(Guid commentId, bool hidden, CancellationToken ct = default)
    {
        var comment = await db.Comments.FirstOrDefaultAsync(c => c.Id == commentId, ct) ?? throw new NotFoundException("Comment", commentId);
        await boulders.RequireStaffAsync(comment.BoulderId, GymRole.Staff, ct);
        if (hidden) comment.Hide(); else comment.Unhide();
        await db.SaveChangesAsync(ct);
        return (await ToDtosAsync([comment], true, ct))[0];
    }

    public async Task<CommentDto> SetLikeAsync(Guid commentId, bool like, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var comment = await db.Comments.AsNoTracking().FirstOrDefaultAsync(c => c.Id == commentId, ct) ?? throw new NotFoundException("Comment", commentId);
        var scope = await boulders.RequireVisibleAsync(comment.BoulderId, ct);
        if (comment.Status == CommentStatus.Hidden && !scope.IsStaff) throw new NotFoundException("Comment", commentId);

        var existing = await db.CommentLikes.FirstOrDefaultAsync(l => l.CommentId == commentId && l.UserId == userId, ct);
        if (like && existing is null) db.CommentLikes.Add(CommentLike.Create(commentId, userId, clock.UtcNow));
        if (!like && existing is not null) db.CommentLikes.Remove(existing);
        try { await db.SaveChangesAsync(ct); }
        catch (UniqueConstraintViolationException) { /* double tap: already liked */ }
        return (await ToDtosAsync([comment], scope.IsStaff, ct))[0];
    }

    private static string Validate(string? content)
    {
        var text = Input.Trimmed(content);
        new Validator()
            .Check(text.Length >= 1, "content", "Write something first.")
            .Check(text.Length <= Comment.MaxLength, "content", $"Keep it to {Comment.MaxLength} characters or fewer.")
            .ThrowIfInvalid();
        return text;
    }

    private async Task<IReadOnlyList<CommentDto>> ToDtosAsync(IReadOnlyList<Comment> comments, bool canModerate, CancellationToken ct)
    {
        var ids = comments.Select(c => c.Id).ToList();
        var authorIds = comments.Select(c => c.UserId).Distinct().ToList();
        var authors = await db.Users.AsNoTracking().Where(u => authorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new PersonDto(u.Id, u.DisplayName, u.AvatarUrl), ct);
        var likes = await db.CommentLikes.AsNoTracking().Where(l => ids.Contains(l.CommentId))
            .GroupBy(l => l.CommentId).Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
        var viewer = currentUser.UserId;
        var liked = viewer is { } uid
            ? (await db.CommentLikes.AsNoTracking().Where(l => l.UserId == uid && ids.Contains(l.CommentId)).Select(l => l.CommentId).ToListAsync(ct)).ToHashSet()
            : [];
        return comments.Select(c => new CommentDto(c.Id, c.BoulderId, authors.GetValueOrDefault(c.UserId, new PersonDto(c.UserId, "Climber", null)),
            c.Content, c.Status, c.CreatedAt, c.EditedAt, likes.GetValueOrDefault(c.Id), liked.Contains(c.Id), c.UserId == viewer, canModerate)).ToList();
    }
}
