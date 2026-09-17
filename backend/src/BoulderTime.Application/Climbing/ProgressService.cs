using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Boulders;
using BoulderTime.Application.Common;
using BoulderTime.Application.Gyms;
using BoulderTime.Domain.Boulders;
using BoulderTime.Domain.Climbing;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Climbing;

public sealed record SetAttemptRequest(int? Attempts, bool? Completed);
public sealed record SetRatingRequest(int? Rating);

/// <summary>
/// A climber's own tracking and rating of a boulder. Works on removed boulders too, so people can log
/// or correct sessions after a retrace. Every rule is scoped to the caller: nobody can touch another user's data.
/// </summary>
public sealed class ProgressService(IAppDbContext db, GymAccess access, ICurrentUser currentUser, IClock clock, BoulderReader reader)
{
    public async Task<ViewerProgressDto?> SetAttemptAsync(Guid boulderId, SetAttemptRequest r, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        await RequireVisibleBoulderAsync(boulderId, ct);
        new Validator()
            .Check(r.Attempts is >= 0 and <= BoulderAttempt.MaxAttempts, "attempts", $"Attempts must be between 0 and {BoulderAttempt.MaxAttempts}.")
            .Check(r.Completed is not null, "completed", "Say whether you completed it.")
            .ThrowIfInvalid();

        var row = await db.BoulderAttempts.FirstOrDefaultAsync(a => a.UserId == userId && a.BoulderId == boulderId, ct);
        if (row is null)
        {
            if (r.Attempts == 0 && r.Completed == false) return null;
            db.BoulderAttempts.Add(row = BoulderAttempt.Start(userId, boulderId));
        }
        row.Record(r.Attempts!.Value, r.Completed!.Value, clock.UtcNow);

        if (row.IsEmpty)
        {
            // Nothing tracked any more: drop the row, and the rating that depended on it.
            db.BoulderAttempts.Remove(row);
            var rating = await db.BoulderRatings.FirstOrDefaultAsync(x => x.UserId == userId && x.BoulderId == boulderId, ct);
            if (rating is not null) db.BoulderRatings.Remove(rating);
        }
        try { await db.SaveChangesAsync(ct); }
        catch (UniqueConstraintViolationException)
        {
            throw new ConflictException("Your progress was updated from another device. Refresh and try again.", "concurrent_update");
        }
        return await ViewerAsync(boulderId, userId, ct);
    }

    public async Task<ViewerProgressDto?> SetRatingAsync(Guid boulderId, SetRatingRequest r, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        await RequireVisibleBoulderAsync(boulderId, ct);
        if (r.Rating is not (>= BoulderRating.Min and <= BoulderRating.Max))
            throw new ValidationException("rating", "Rate from 1 to 5 stars.");
        if (!await db.BoulderAttempts.AnyAsync(a => a.UserId == userId && a.BoulderId == boulderId && a.Attempts > 0, ct))
            throw new ForbiddenException("Try the boulder before rating it.", "attempt_required");

        var rating = await db.BoulderRatings.FirstOrDefaultAsync(x => x.UserId == userId && x.BoulderId == boulderId, ct);
        if (rating is null) db.BoulderRatings.Add(BoulderRating.Create(userId, boulderId, r.Rating.Value));
        else rating.Change(r.Rating.Value);
        await db.SaveChangesAsync(ct);
        return await ViewerAsync(boulderId, userId, ct);
    }

    public async Task<ViewerProgressDto?> ClearRatingAsync(Guid boulderId, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var rating = await db.BoulderRatings.FirstOrDefaultAsync(x => x.UserId == userId && x.BoulderId == boulderId, ct);
        if (rating is not null)
        {
            db.BoulderRatings.Remove(rating);
            await db.SaveChangesAsync(ct);
        }
        return await ViewerAsync(boulderId, userId, ct);
    }

    private async Task<ViewerProgressDto?> ViewerAsync(Guid boulderId, Guid userId, CancellationToken ct) =>
        (await reader.ViewerAsync([boulderId], userId, ct)).GetValueOrDefault(boulderId);

    private async Task RequireVisibleBoulderAsync(Guid boulderId, CancellationToken ct)
    {
        var b = await db.Boulders.AsNoTracking().Where(x => x.Id == boulderId)
            .Join(db.Gyms, x => x.GymId, g => g.Id, (x, g) => new { g.Id, g.Status })
            .FirstOrDefaultAsync(ct) ?? throw new NotFoundException("Boulder", boulderId);
        if (b.Status != Domain.Gyms.GymStatus.Active && await access.GetRoleAsync(b.Id, ct) is null)
            throw new NotFoundException("Boulder", boulderId);
    }
}
