using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Common;
using BoulderTime.Application.Gyms;
using BoulderTime.Application.Users;
using BoulderTime.Domain.Staff;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Application.Images;

public enum GymImageKind { Logo = 0, Cover = 1 }

public sealed record ImageUploadRequest(string? ContentType, long? SizeBytes);
public sealed record GymImageUploadRequest(GymImageKind? Kind, string? ContentType, long? SizeBytes);
/// <summary>Set to an uploaded path, or null to remove the image.</summary>
public sealed record SetImageRequest(string? Path);

/// <summary>
/// Profile avatars and gym logo/cover images. Files go to public buckets through upload tickets; the API verifies each
/// object exists under the owner's folder before using it, and deletes replaced files.
/// </summary>
public sealed class ImageService(IAppDbContext db, GymAccess access, IObjectStorage storage, ICurrentUser currentUser, UserService users)
{
    public const long MaxAvatarBytes = 2 * 1024 * 1024;
    public const long MaxGymImageBytes = 5 * 1024 * 1024;
    private static readonly Dictionary<string, string> Types = new(StringComparer.OrdinalIgnoreCase) { ["image/jpeg"] = "jpg", ["image/png"] = "png", ["image/webp"] = "webp" };

    public static string AvatarPrefix(Guid userId) => $"users/{userId}/avatar/";
    public static string GymImagePrefix(Guid gymId, GymImageKind kind) => $"gyms/{gymId}/{(kind == GymImageKind.Logo ? "logo" : "cover")}/";

    public async Task<UploadTicket> CreateAvatarUploadAsync(ImageUploadRequest r, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        Validate(r.ContentType, r.SizeBytes, MaxAvatarBytes);
        var path = $"{AvatarPrefix(userId)}{Guid.NewGuid():N}.{Types[r.ContentType!]}";
        return await storage.CreateUploadTicketAsync(StorageBuckets.Avatars, path, r.ContentType!.ToLowerInvariant(), MaxAvatarBytes, ct) with { Resumable = null };
    }

    public async Task<CurrentUserDto> SetAvatarAsync(SetImageRequest r, CancellationToken ct = default)
    {
        var userId = currentUser.RequireUserId();
        var user = await db.Users.FirstAsync(u => u.Id == userId, ct);
        var path = await RequireOwnedAsync(StorageBuckets.Avatars, AvatarPrefix(userId), r.Path, user.AvatarPath, ct);
        var previous = user.SetAvatar(path is null ? null : storage.PublicUrl(StorageBuckets.Avatars, path), path);
        await db.SaveChangesAsync(ct);
        if (previous is not null) await storage.DeleteAsync(StorageBuckets.Avatars, previous, ct);
        return await users.GetAsync(userId, ct);
    }

    public async Task<UploadTicket> CreateGymImageUploadAsync(Guid gymId, GymImageUploadRequest r, CancellationToken ct = default)
    {
        await access.RequireRoleAsync(gymId, GymRole.Admin, ct);
        if (r.Kind is not { } kind || !Enum.IsDefined(kind)) throw new ValidationException("kind", "Choose logo or cover.");
        Validate(r.ContentType, r.SizeBytes, MaxGymImageBytes);
        var path = $"{GymImagePrefix(gymId, kind)}{Guid.NewGuid():N}.{Types[r.ContentType!]}";
        return await storage.CreateUploadTicketAsync(StorageBuckets.GymImages, path, r.ContentType!.ToLowerInvariant(), MaxGymImageBytes, ct) with { Resumable = null };
    }

    public async Task<GymDetailDto> SetGymImageAsync(Guid gymId, GymImageKind kind, SetImageRequest r, CancellationToken ct = default)
    {
        var (gym, role) = await access.RequireRoleAsync(gymId, GymRole.Admin, ct);
        var current = kind == GymImageKind.Logo ? gym.LogoPath : gym.CoverImagePath;
        var path = await RequireOwnedAsync(StorageBuckets.GymImages, GymImagePrefix(gymId, kind), r.Path, current, ct);
        var url = path is null ? null : storage.PublicUrl(StorageBuckets.GymImages, path);
        var previous = kind == GymImageKind.Logo ? gym.SetLogo(url, path) : gym.SetCover(url, path);
        await db.SaveChangesAsync(ct);
        if (previous is not null) await storage.DeleteAsync(StorageBuckets.GymImages, previous, ct);
        return GymDetailDto.From(gym, role);
    }

    private static void Validate(string? contentType, long? size, long max) =>
        new Validator()
            .Check(contentType is not null && Types.ContainsKey(contentType), "contentType", "Upload a JPEG, PNG or WebP image.")
            .Check(size is > 0 && size <= max, "sizeBytes", $"Images must be under {max / 1024 / 1024} MB.")
            .ThrowIfInvalid();

    /// <summary>Null means "remove". Otherwise the object must exist under the owner's folder.</summary>
    private async Task<string?> RequireOwnedAsync(string bucket, string prefix, string? requested, string? current, CancellationToken ct)
    {
        var path = Input.Trimmed(requested);
        if (path.Length == 0) return null;
        if (path == current) return path;
        var ok = path.StartsWith(prefix, StringComparison.Ordinal) && !path.Contains("..") && !path.Contains('\\') && await storage.ExistsAsync(bucket, path, ct);
        if (!ok) throw new ValidationException("path", "The image upload didn't complete. Upload it again.");
        return path;
    }
}
