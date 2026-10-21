using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Boulders;
using BoulderTime.Application.Common;
using BoulderTime.Application.Gyms;
using BoulderTime.Domain.Boulders;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BoulderTime.Application.Admin;

/// <param name="Path">The file the boulder has now, to read with <c>file</c> and replace.</param>
public sealed record RecompressionCandidateDto(Guid BoulderId, PhotoFile File, string Path);

/// <param name="Pending">The pictures not made lighter yet.</param>
/// <param name="Replaced">Replaced and not yet confirmed: each one can still be undone.</param>
/// <param name="BytesBefore">What the replaced pictures weighed.</param>
/// <param name="BytesAfter">What their lighter copies weigh.</param>
/// <param name="Confirmed">Replaced and confirmed, old files gone.</param>
public sealed record RecompressionOverviewDto(IReadOnlyList<RecompressionCandidateDto> Pending, int Replaced, long BytesBefore, long BytesAfter, int Confirmed);

public sealed record RecompressionUploadRequest(PhotoFile? File, long? SizeBytes);

/// <param name="From">The file the copy was made from, which the boulder must still have.</param>
/// <param name="To">The uploaded copy.</param>
public sealed record ApplyRecompressionRequest(Guid? BoulderId, PhotoFile? File, string? From, string? To);

public sealed record AppliedRecompressionDto(long BytesBefore, long BytesAfter);

/// <param name="Changed">Pictures put back (undo) or whose old file was deleted (confirm).</param>
/// <param name="Skipped">Undo only: pictures staff changed since, which keep the new picture they have.</param>
public sealed record RecompressionResultDto(int Changed, int Skipped, long BytesFreed);

/// <summary>
/// Makes a gym's existing boulder pictures lighter, the way the app now prepares new ones. The pictures are encoded
/// again on the administrator's device (the server has no image library), uploaded as new files and swapped in one
/// by one; the old files stay, so the whole gym can be put back as it was. Confirming deletes the old files.
///
/// Only the boulders' own photos and card pictures: wall photos are shared and stay as they are, and the original
/// a highlight goes on from must keep matching its mask pixel for pixel. Platform administrators only.
/// </summary>
public sealed class PhotoRecompressionService(IAppDbContext db, IObjectStorage storage, GymAccess access, ICurrentUser currentUser, IClock clock,
    ILogger<PhotoRecompressionService> logger)
{
    private const string Bucket = StorageBuckets.BoulderImages;

    public async Task<RecompressionOverviewDto> OverviewAsync(Guid gymId, CancellationToken ct = default)
    {
        await RequireGymAsync(gymId, ct);
        var done = await db.PhotoRecompressions.AsNoTracking().Where(r => r.GymId == gymId).ToListAsync(ct);
        var made = done.Where(r => r.RolledBackAt is null).Select(r => r.NewPath).ToHashSet(StringComparer.Ordinal);
        var boulders = await db.Boulders.AsNoTracking().Where(b => b.GymId == gymId && b.Status != BoulderStatus.Deleted)
            .OrderBy(b => b.CreatedAt).ToListAsync(ct);

        var pending = new List<RecompressionCandidateDto>();
        foreach (var b in boulders)
        {
            if (IsOwnPhoto(gymId, b) && !made.Contains(b.PhotoPath)) pending.Add(new(b.Id, PhotoFile.Photo, b.PhotoPath));
            if (b.ThumbnailPath is { } thumb && IsOwn(gymId, thumb) && !made.Contains(thumb)) pending.Add(new(b.Id, PhotoFile.Thumbnail, thumb));
        }
        var open = done.Where(r => r.IsOpen).ToList();
        return new RecompressionOverviewDto(pending, open.Count, open.Sum(r => r.OldBytes), open.Sum(r => r.NewBytes), done.Count(r => r.FinalizedAt is not null));
    }

    /// <summary>The picture a boulder has now, through the API, so the device may read its pixels.</summary>
    public async Task<(byte[] Content, string ContentType)> FileAsync(Guid gymId, Guid boulderId, PhotoFile file, CancellationToken ct = default)
    {
        await RequireGymAsync(gymId, ct);
        var boulder = await db.Boulders.AsNoTracking().FirstOrDefaultAsync(b => b.Id == boulderId && b.GymId == gymId && b.Status != BoulderStatus.Deleted, ct)
                      ?? throw new NotFoundException("Boulder", boulderId);
        var path = file == PhotoFile.Photo ? (IsOwnPhoto(gymId, boulder) ? boulder.PhotoPath : null) : boulder.ThumbnailPath;
        var bytes = path is null || !IsOwn(gymId, path) ? null : await storage.ReadAsync(Bucket, path, ct);
        if (bytes is null) throw new NotFoundException("Picture", boulderId);
        var type = Path.GetExtension(path!).ToLowerInvariant() switch { ".png" => "image/png", ".webp" => "image/webp", _ => "image/jpeg" };
        return (bytes, type);
    }

    public async Task<UploadTicket> CreateUploadAsync(Guid gymId, RecompressionUploadRequest r, CancellationToken ct = default)
    {
        await RequireGymAsync(gymId, ct);
        var max = r.File == PhotoFile.Thumbnail ? BoulderService.MaxThumbnailBytes : BoulderService.MaxPhotoBytes;
        new Validator()
            .Check(r.File is not null, "file", "Say which picture.")
            .Check(r.SizeBytes is > 0 && r.SizeBytes <= max, "sizeBytes", "The picture is too large.")
            .ThrowIfInvalid();
        var path = $"{BoulderService.PhotoPrefix(gymId)}{Guid.NewGuid():N}{(r.File == PhotoFile.Thumbnail ? ".thumb" : "")}.jpg";
        return await storage.CreateUploadTicketAsync(Bucket, path, "image/jpeg", max, ct) with { Resumable = null };
    }

    /// <summary>
    /// Swaps the uploaded copy in. Refused, and the copy deleted, when it saves nothing or the boulder no longer has
    /// the file it was made from (staff changed it meanwhile).
    /// </summary>
    public async Task<AppliedRecompressionDto> ApplyAsync(Guid gymId, ApplyRecompressionRequest r, CancellationToken ct = default)
    {
        await RequireGymAsync(gymId, ct);
        var from = Input.Trimmed(r.From);
        var to = Input.Trimmed(r.To);
        var thumbnail = r.File == PhotoFile.Thumbnail;
        new Validator()
            .Check(r.BoulderId is not null && r.File is not null, "file", "Say which picture.")
            .Check(IsOwn(gymId, to) && to.EndsWith(thumbnail ? ".thumb.jpg" : ".jpg", StringComparison.Ordinal)
                   && (thumbnail || !to.Contains(".thumb.")), "to", "The image upload didn't complete. Upload it again.")
            .ThrowIfInvalid();

        var boulder = await db.Boulders.FirstOrDefaultAsync(b => b.Id == r.BoulderId && b.GymId == gymId && b.Status != BoulderStatus.Deleted, ct)
                      ?? throw new NotFoundException("Boulder", r.BoulderId!);
        var newBytes = (await storage.ReadAsync(Bucket, to, ct))?.LongLength
                       ?? throw new ValidationException("to", "The image upload didn't complete. Upload it again.");
        var oldBytes = IsOwn(gymId, from) ? (await storage.ReadAsync(Bucket, from, ct))?.LongLength : null;

        string? refusal = oldBytes is null ? "This picture has changed meanwhile. Load the list again."
            : newBytes >= oldBytes ? "The lighter copy is not lighter: the picture stays as it is."
            : !boulder.SwapFile(r.File!.Value, from, to) ? "This picture has changed meanwhile. Load the list again."
            : null;
        if (refusal is not null)
        {
            await storage.DeleteAsync(Bucket, to, ct);
            throw new ConflictException(refusal);
        }
        db.PhotoRecompressions.Add(PhotoRecompression.Create(gymId, boulder.Id, r.File!.Value, from, to, oldBytes!.Value, newBytes, currentUser.UserId, clock.UtcNow));
        await db.SaveChangesAsync(ct);
        return new AppliedRecompressionDto(oldBytes!.Value, newBytes);
    }

    /// <summary>Puts every picture of the gym not yet confirmed back to its old file, and deletes the lighter copies.</summary>
    public async Task<RecompressionResultDto> RollbackAsync(Guid gymId, BetaTestRequest r, CancellationToken ct = default)
    {
        await RequireConfirmedAsync(gymId, r, ct);
        var open = await db.PhotoRecompressions.Where(x => x.GymId == gymId && x.RolledBackAt == null && x.FinalizedAt == null)
            .OrderByDescending(x => x.CreatedAt).ToListAsync(ct);
        var ids = open.Select(x => x.BoulderId).Distinct().ToList();
        var boulders = await db.Boulders.Where(b => ids.Contains(b.Id)).ToDictionaryAsync(b => b.Id, ct);
        var now = clock.UtcNow;
        var restored = new List<PhotoRecompression>();
        var skipped = 0;
        foreach (var x in open)
        {
            // Staff gave it another picture since (or deleted it): that picture stays; the old file is left for confirm.
            if (!boulders.TryGetValue(x.BoulderId, out var b) || b.Status == BoulderStatus.Deleted || !b.SwapFile(x.File, x.NewPath, x.OldPath))
            {
                skipped++;
                continue;
            }
            x.RolledBack(now);
            restored.Add(x);
        }
        await db.SaveChangesAsync(ct);
        foreach (var x in restored) await TryDeleteAsync(x.NewPath, ct);
        logger.LogInformation("Platform admin {UserId} put back {Restored} pictures of gym {GymId} ({Skipped} changed since, left as they are)",
            currentUser.UserId, restored.Count, gymId, skipped);
        return new RecompressionResultDto(restored.Count, skipped, restored.Sum(x => x.NewBytes));
    }

    /// <summary>Deletes the old files of the gym's replaced pictures. There is no undoing it.</summary>
    public async Task<RecompressionResultDto> FinalizeAsync(Guid gymId, BetaTestRequest r, CancellationToken ct = default)
    {
        await RequireConfirmedAsync(gymId, r, ct);
        var open = await db.PhotoRecompressions.Where(x => x.GymId == gymId && x.RolledBackAt == null && x.FinalizedAt == null).ToListAsync(ct);
        var old = open.Select(x => x.OldPath).ToList();
        // Never a file something still uses.
        var used = (await db.Boulders.AsNoTracking()
                .Where(b => old.Contains(b.PhotoPath) || old.Contains(b.ThumbnailPath!) || old.Contains(b.HighlightSourcePath!))
                .Select(b => new { b.PhotoPath, b.ThumbnailPath, b.HighlightSourcePath }).ToListAsync(ct))
            .SelectMany(b => new[] { b.PhotoPath, b.ThumbnailPath, b.HighlightSourcePath }).OfType<string>().ToHashSet(StringComparer.Ordinal);
        var now = clock.UtcNow;
        foreach (var x in open) x.Finalized(now);
        await db.SaveChangesAsync(ct);
        long freed = 0;
        foreach (var x in open.Where(x => !used.Contains(x.OldPath)))
        {
            await TryDeleteAsync(x.OldPath, ct);
            freed += x.OldBytes;
        }
        logger.LogInformation("Platform admin {UserId} confirmed {Count} lighter pictures of gym {GymId}, {Freed} bytes freed",
            currentUser.UserId, open.Count, gymId, freed);
        return new RecompressionResultDto(open.Count, 0, freed);
    }

    private static bool IsOwn(Guid gymId, string path) =>
        path.StartsWith(BoulderService.PhotoPrefix(gymId), StringComparison.Ordinal) && !path.Contains("..") && !path.Contains('\\') && !path.Contains(".mask.");

    private static bool IsOwnPhoto(Guid gymId, Boulder b) =>
        IsOwn(gymId, b.PhotoPath) && !b.PhotoPath.Contains(".thumb.") && b.PhotoPath != b.HighlightSourcePath;

    private async Task RequireGymAsync(Guid gymId, CancellationToken ct)
    {
        await access.RequirePlatformAdminAsync(ct);
        if (!await db.Gyms.AnyAsync(g => g.Id == gymId, ct)) throw new NotFoundException("Gym", gymId);
    }

    /// <summary>A platform administrator, and the gym's slug typed again, as for every action that touches a whole gym.</summary>
    private async Task RequireConfirmedAsync(Guid gymId, BetaTestRequest r, CancellationToken ct)
    {
        await access.RequirePlatformAdminAsync(ct);
        var gym = await db.Gyms.AsNoTracking().FirstOrDefaultAsync(g => g.Id == gymId, ct) ?? throw new NotFoundException("Gym", gymId);
        if (!string.Equals(Input.Trimmed(r.GymSlug), gym.Slug, StringComparison.Ordinal))
            throw new ValidationException("gymSlug", "Type the gym's slug to confirm.");
    }

    private async Task TryDeleteAsync(string path, CancellationToken ct)
    {
        try { await storage.DeleteAsync(Bucket, path, ct); }
        catch (Exception ex) { logger.LogWarning(ex, "Couldn't delete {Path} from storage", path); }
    }
}
