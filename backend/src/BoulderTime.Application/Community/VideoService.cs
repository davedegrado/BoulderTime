using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Boulders;
using BoulderTime.Application.Common;
using BoulderTime.Application.Gyms;
using BoulderTime.Domain.Community;
using BoulderTime.Domain.Staff;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Community;

public enum VideoKind { Community = 0, Beta = 1, CommunityThumbnail = 2, BetaThumbnail = 3 }

public sealed record VideoUploadRequest(VideoKind? Kind, string? ContentType, long? SizeBytes);

public sealed record BetaDto(Guid Id, Guid BoulderId, string VideoUrl, string? ThumbnailUrl, string? Caption, PersonDto UploadedBy, DateTimeOffset UpdatedAt);
public sealed record SaveBetaRequest(string? StoragePath, string? Caption, string? ThumbnailPath = null);

public sealed record VideoDto(
    Guid Id, Guid BoulderId, PersonDto Author, string VideoUrl, string? ThumbnailUrl, string? Caption, VideoStatus Status,
    string? RejectionReason, DateTimeOffset CreatedAt, bool IsMine);

/// <summary>Approved videos are paged (a popular boulder can have many); the viewer's own unapproved videos come separately.</summary>
public sealed record BoulderVideosDto(PagedResult<VideoDto> Approved, IReadOnlyList<VideoDto> MineInReview);

public sealed record SubmitVideoRequest(string? StoragePath, string? Caption, string? ThumbnailPath = null);
public sealed record ModifyVideoRequest(string? StoragePath, string? Caption, string? ThumbnailPath = null);
public sealed record RejectVideoRequest(string? Reason);

public sealed record ModerationVideoDto(VideoDto Video, BoulderSummaryDto Boulder);

/// <summary>
/// Official beta (staff, unmoderated, one per boulder) and community videos (moderated).
/// Both live in PRIVATE buckets; every URL handed out is short-lived and only issued to viewers allowed to see the video:
/// approved videos → anyone who can see the boulder; pending/rejected → the uploader and the gym's staff.
/// </summary>
public sealed class VideoService(IAppDbContext db, BoulderAccess boulders, GymAccess gyms, IObjectStorage storage, ICurrentUser currentUser, IClock clock, BoulderReader reader, Notifications.NotificationPublisher notifications)
{
    public const long MaxVideoBytes = 100 * 1024 * 1024;
    public const long MaxThumbnailBytes = 1024 * 1024;
    private static readonly Dictionary<string, string> ThumbnailTypes = new(StringComparer.OrdinalIgnoreCase) { ["image/jpeg"] = "jpg", ["image/webp"] = "webp" };
    public const int MaxVideosPerUserPerBoulder = 3;
    public static readonly TimeSpan ReadUrlLifetime = TimeSpan.FromHours(1);
    private static readonly Dictionary<string, string> VideoTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["video/mp4"] = "mp4", ["video/quicktime"] = "mov", ["video/webm"] = "webm",
    };

    private static bool IsBeta(VideoKind kind) => kind is VideoKind.Beta or VideoKind.BetaThumbnail;
    private static bool IsThumbnail(VideoKind kind) => kind is VideoKind.CommunityThumbnail or VideoKind.BetaThumbnail;

    public static string Prefix(Guid gymId, Guid boulderId, VideoKind kind) =>
        $"gyms/{gymId}/boulders/{boulderId}/{(IsBeta(kind) ? "beta" : "videos")}/";

    private static string Bucket(VideoKind kind) => IsBeta(kind) ? StorageBuckets.OfficialBeta : StorageBuckets.CommunityVideos;

    // ---------- Uploads ----------

    public async Task<UploadTicket> CreateUploadAsync(Guid boulderId, VideoUploadRequest r, CancellationToken ct = default)
    {
        currentUser.RequireUserId();
        var kind = r.Kind ?? VideoKind.Community;
        if (!Enum.IsDefined(kind)) throw new ValidationException("kind", "Unknown upload kind.");
        var scope = IsBeta(kind)
            ? await boulders.RequireStaffAsync(boulderId, GymRole.Staff, ct)
            : await boulders.RequireVisibleAsync(boulderId, ct);

        if (IsThumbnail(kind))
        {
            new Validator()
                .Check(r.ContentType is not null && ThumbnailTypes.ContainsKey(r.ContentType), "contentType", "Thumbnails must be JPEG or WebP.")
                .Check(r.SizeBytes is > 0 and <= MaxThumbnailBytes, "sizeBytes", "Thumbnails must be under 1 MB.")
                .ThrowIfInvalid();
            var thumbPath = $"{Prefix(scope.Gym.Id, boulderId, kind)}{Guid.NewGuid():N}.thumb.{ThumbnailTypes[r.ContentType!]}";
            return await storage.CreateUploadTicketAsync(Bucket(kind), thumbPath, r.ContentType!.ToLowerInvariant(), MaxThumbnailBytes, ct) with { Resumable = null };
        }

        new Validator()
            .Check(r.ContentType is not null && VideoTypes.ContainsKey(r.ContentType), "contentType", "Upload an MP4, MOV or WebM video.")
            .Check(r.SizeBytes is > 0 and <= MaxVideoBytes, "sizeBytes", $"Videos must be under {MaxVideoBytes / 1024 / 1024} MB.")
            .ThrowIfInvalid();
        var path = $"{Prefix(scope.Gym.Id, boulderId, kind)}{Guid.NewGuid():N}.{VideoTypes[r.ContentType!]}";
        return await storage.CreateUploadTicketAsync(Bucket(kind), path, r.ContentType!.ToLowerInvariant(), MaxVideoBytes, ct);
    }

    // ---------- Official beta ----------

    public async Task<BetaDto?> GetBetaAsync(Guid boulderId, CancellationToken ct = default)
    {
        await boulders.RequireVisibleAsync(boulderId, ct);
        var beta = await db.BoulderBetas.AsNoTracking().FirstOrDefaultAsync(b => b.BoulderId == boulderId, ct);
        return beta is null ? null : await ToBetaAsync(beta, ct);
    }

    public async Task<BetaDto> SaveBetaAsync(Guid boulderId, SaveBetaRequest r, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var scope = await boulders.RequireStaffAsync(boulderId, GymRole.Staff, ct);
        var beta = await db.BoulderBetas.FirstOrDefaultAsync(b => b.BoulderId == boulderId, ct);
        var path = Input.Trimmed(r.StoragePath);
        if (path != beta?.StoragePath) await RequireUploadedAsync(VideoKind.Beta, scope, path, ct);
        var thumb = await OptionalThumbnailAsync(VideoKind.BetaThumbnail, scope, r.ThumbnailPath, beta?.ThumbnailPath, ct);
        ValidateCaption(r.Caption, BoulderBeta.CaptionMaxLength);

        IReadOnlyList<string> replaced = [];
        var isNewVideo = beta is null || beta.StoragePath != path;
        if (beta is null) db.BoulderBetas.Add(beta = BoulderBeta.Create(boulderId, userId, path, thumb, r.Caption));
        else replaced = beta.Replace(userId, path, thumb, r.Caption);
        if (isNewVideo)
        {
            var sectorName = await db.Sectors.AsNoTracking().Where(x => x.Id == scope.Boulder.SectorId).Select(x => x.Name).FirstAsync(ct);
            await notifications.OfficialBetaAsync(scope.Boulder, scope.Gym, sectorName, userId, ct);
        }
        await db.SaveChangesAsync(ct);
        foreach (var old in replaced) await storage.DeleteAsync(StorageBuckets.OfficialBeta, old, ct);
        return await ToBetaAsync(beta, ct);
    }

    public async Task DeleteBetaAsync(Guid boulderId, CancellationToken ct = default)
    {
        await boulders.RequireStaffAsync(boulderId, GymRole.Staff, ct);
        var beta = await db.BoulderBetas.FirstOrDefaultAsync(b => b.BoulderId == boulderId, ct);
        if (beta is null) return;
        db.BoulderBetas.Remove(beta);
        await db.SaveChangesAsync(ct);
        await storage.DeleteAsync(StorageBuckets.OfficialBeta, beta.StoragePath, ct);
        if (beta.ThumbnailPath is not null) await storage.DeleteAsync(StorageBuckets.OfficialBeta, beta.ThumbnailPath, ct);
    }

    // ---------- Community videos ----------

    public const int VideosPageSize = 12;

    /// <summary>
    /// Approved videos, newest first and paged. The viewer's own pending/rejected videos are returned separately so they
    /// can be shown with their review status. Staff review pending videos in the moderation queue.
    /// </summary>
    public async Task<BoulderVideosDto> ListAsync(Guid boulderId, int? page, int? pageSize, CancellationToken ct = default)
    {
        await boulders.RequireVisibleAsync(boulderId, ct);
        var (p, size) = Paging.Normalize(page, pageSize, VideosPageSize);
        var approved = db.BoulderVideos.AsNoTracking().Where(v => v.BoulderId == boulderId && v.Status == VideoStatus.Approved);
        var total = await approved.CountAsync(ct);
        var rows = await approved.OrderByDescending(v => v.ReviewedAt).ThenByDescending(v => v.CreatedAt).Skip((p - 1) * size).Take(size).ToListAsync(ct);

        IReadOnlyList<VideoDto> mine = [];
        if (currentUser.UserId is { } uid && p == 1)
        {
            var own = await db.BoulderVideos.AsNoTracking()
                .Where(v => v.BoulderId == boulderId && v.UserId == uid && v.Status != VideoStatus.Approved)
                .OrderByDescending(v => v.UpdatedAt).ToListAsync(ct);
            mine = await ToVideosAsync(own, ct);
        }
        return new BoulderVideosDto(new PagedResult<VideoDto>(await ToVideosAsync(rows, ct), p, size, total), mine);
    }

    public async Task<VideoDto> SubmitAsync(Guid boulderId, SubmitVideoRequest r, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var scope = await boulders.RequireVisibleAsync(boulderId, ct);
        var path = Input.Trimmed(r.StoragePath);
        await RequireUploadedAsync(VideoKind.Community, scope, path, ct);
        var thumb = await OptionalThumbnailAsync(VideoKind.CommunityThumbnail, scope, r.ThumbnailPath, null, ct);
        ValidateCaption(r.Caption, BoulderVideo.CaptionMaxLength);
        if (await db.BoulderVideos.CountAsync(v => v.BoulderId == boulderId && v.UserId == userId, ct) >= MaxVideosPerUserPerBoulder)
            throw new ConflictException($"You can post up to {MaxVideosPerUserPerBoulder} videos per boulder. Delete one to add another.", "too_many_videos");

        var video = BoulderVideo.Submit(boulderId, userId, path, thumb, r.Caption);
        db.BoulderVideos.Add(video);
        await db.SaveChangesAsync(ct);
        return (await ToVideosAsync([video], ct))[0];
    }

    /// <summary>Only the author can modify; any modification (caption or file) sends the video back to review.</summary>
    public async Task<VideoDto> ModifyAsync(Guid videoId, ModifyVideoRequest r, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var video = await db.BoulderVideos.FirstOrDefaultAsync(v => v.Id == videoId, ct) ?? throw new NotFoundException("Video", videoId);
        if (video.UserId != userId) throw new NotFoundException("Video", videoId);
        var scope = await boulders.RequireVisibleAsync(video.BoulderId, ct);
        var path = r.StoragePath is null ? null : Input.Trimmed(r.StoragePath);
        string? thumb = null;
        if (path is not null && path != video.StoragePath)
        {
            await RequireUploadedAsync(VideoKind.Community, scope, path, ct);
            thumb = await OptionalThumbnailAsync(VideoKind.CommunityThumbnail, scope, r.ThumbnailPath, null, ct);
        }
        if (r.Caption is not null) ValidateCaption(r.Caption, BoulderVideo.CaptionMaxLength);

        var replaced = video.Modify(r.Caption, path, thumb);
        await db.SaveChangesAsync(ct);
        foreach (var old in replaced) await storage.DeleteAsync(StorageBuckets.CommunityVideos, old, ct);
        return (await ToVideosAsync([video], ct))[0];
    }

    /// <summary>Authors can always delete their own video, whatever its review state.</summary>
    public async Task DeleteAsync(Guid videoId, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var video = await db.BoulderVideos.FirstOrDefaultAsync(v => v.Id == videoId, ct) ?? throw new NotFoundException("Video", videoId);
        if (video.UserId != userId) throw new NotFoundException("Video", videoId);
        db.BoulderVideos.Remove(video);
        await db.SaveChangesAsync(ct);
        await storage.DeleteAsync(StorageBuckets.CommunityVideos, video.StoragePath, ct);
        if (video.ThumbnailPath is not null) await storage.DeleteAsync(StorageBuckets.CommunityVideos, video.ThumbnailPath, ct);
    }

    // ---------- Moderation ----------

    public async Task<IReadOnlyList<ModerationVideoDto>> PendingForGymAsync(Guid gymId, CancellationToken ct = default)
    {
        await gyms.RequireRoleAsync(gymId, GymRole.Staff, ct);
        var rows = await db.BoulderVideos.AsNoTracking()
            .Where(v => v.Status == VideoStatus.Pending)
            .Join(db.Boulders, v => v.BoulderId, b => b.Id, (v, b) => new { v, b })
            .Where(x => x.b.GymId == gymId)
            .OrderBy(x => x.v.UpdatedAt)
            .Take(100)
            .ToListAsync(ct);
        var videos = await ToVideosAsync(rows.Select(x => x.v).ToList(), ct);
        var summaries = (await reader.SummariesAsync(rows.Select(x => x.b).DistinctBy(b => b.Id).ToList(), ct)).ToDictionary(s => s.Id);
        return videos.Select(v => new ModerationVideoDto(v, summaries[v.BoulderId])).ToList();
    }

    public async Task<VideoDto> ApproveAsync(Guid videoId, CancellationToken ct = default) =>
        await ReviewAsync(videoId, (v, reviewer, now) => v.Approve(reviewer, now), ct);

    public async Task<VideoDto> RejectAsync(Guid videoId, RejectVideoRequest r, CancellationToken ct = default)
    {
        var reason = Input.Trimmed(r.Reason);
        new Validator()
            .Check(reason.Length >= 3, "reason", "Tell the climber why, so they can fix it.")
            .Check(reason.Length <= BoulderVideo.ReasonMaxLength, "reason", $"Keep it to {BoulderVideo.ReasonMaxLength} characters or fewer.")
            .ThrowIfInvalid();
        return await ReviewAsync(videoId, (v, reviewer, now) => v.Reject(reviewer, now, reason), ct);
    }

    internal async Task<VideoDto> ReviewAsync(Guid videoId, Action<BoulderVideo, Guid, DateTimeOffset> decide, CancellationToken ct)
    {
        var reviewer = currentUser.RequireUserId();
        var video = await db.BoulderVideos.FirstOrDefaultAsync(v => v.Id == videoId, ct) ?? throw new NotFoundException("Video", videoId);
        var scope = await boulders.RequireStaffAsync(video.BoulderId, GymRole.Staff, ct);
        if (video.UserId == reviewer)
            throw new ForbiddenException("You can't review your own video. Another staff member has to.", "own_video");
        decide(video, reviewer, clock.UtcNow);
        await notifications.VideoReviewedAsync(video, scope.Gym.Id, reviewer, ct);
        await db.SaveChangesAsync(ct);
        return (await ToVideosAsync([video], ct))[0];
    }

    // ---------- Helpers ----------

    private async Task RequireUploadedAsync(VideoKind kind, BoulderScope scope, string path, CancellationToken ct)
    {
        var wellFormed = path.StartsWith(Prefix(scope.Gym.Id, scope.Boulder.Id, kind), StringComparison.Ordinal) && !path.Contains("..") && !path.Contains('\\');
        if (!wellFormed || !await storage.ExistsAsync(Bucket(kind), path, ct))
            throw new ValidationException("storagePath", "The video upload didn't complete. Upload it again.");
    }

    /// <summary>A thumbnail is optional; when given it must be an uploaded image in the same boulder folder.</summary>
    private async Task<string?> OptionalThumbnailAsync(VideoKind kind, BoulderScope scope, string? requested, string? current, CancellationToken ct)
    {
        var path = Input.Trimmed(requested);
        if (path.Length == 0) return null;
        if (path == current) return path;
        var ok = path.StartsWith(Prefix(scope.Gym.Id, scope.Boulder.Id, kind), StringComparison.Ordinal) && path.Contains(".thumb.")
                 && !path.Contains("..") && await storage.ExistsAsync(Bucket(kind), path, ct);
        if (!ok) throw new ValidationException("thumbnailPath", "The thumbnail upload didn't complete. Upload the video again.");
        return path;
    }

    private static void ValidateCaption(string? caption, int max)
    {
        if (Input.Trimmed(caption).Length > max) throw new ValidationException("caption", $"Keep the caption to {max} characters or fewer.");
    }

    private async Task<BetaDto> ToBetaAsync(BoulderBeta beta, CancellationToken ct)
    {
        var person = await db.Users.AsNoTracking().Where(u => u.Id == beta.UploadedByUserId).Select(u => new PersonDto(u.Id, u.DisplayName, u.AvatarUrl)).FirstAsync(ct);
        var url = await storage.CreateReadUrlAsync(StorageBuckets.OfficialBeta, beta.StoragePath, ReadUrlLifetime, ct);
        var thumb = beta.ThumbnailPath is null ? null : await storage.CreateReadUrlAsync(StorageBuckets.OfficialBeta, beta.ThumbnailPath, ReadUrlLifetime, ct);
        return new BetaDto(beta.Id, beta.BoulderId, url, thumb, beta.Caption, person, beta.UpdatedAt);
    }

    private async Task<IReadOnlyList<VideoDto>> ToVideosAsync(IReadOnlyList<BoulderVideo> videos, CancellationToken ct)
    {
        var authorIds = videos.Select(v => v.UserId).Distinct().ToList();
        var authors = await db.Users.AsNoTracking().Where(u => authorIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => new PersonDto(u.Id, u.DisplayName, u.AvatarUrl), ct);
        var viewer = currentUser.UserId;
        var result = new List<VideoDto>(videos.Count);
        foreach (var v in videos)
        {
            var url = await storage.CreateReadUrlAsync(StorageBuckets.CommunityVideos, v.StoragePath, ReadUrlLifetime, ct);
            var thumb = v.ThumbnailPath is null ? null : await storage.CreateReadUrlAsync(StorageBuckets.CommunityVideos, v.ThumbnailPath, ReadUrlLifetime, ct);
            result.Add(new VideoDto(v.Id, v.BoulderId, authors.GetValueOrDefault(v.UserId, new PersonDto(v.UserId, "Climber", null)), url, thumb,
                v.Caption, v.Status, v.UserId == viewer ? v.RejectionReason : null, v.CreatedAt, v.UserId == viewer));
        }
        return result;
    }
}
