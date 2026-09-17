namespace BoulderTime.Application.Abstractions;

public static class StorageBuckets
{
    public const string BoulderImages = "boulder-images";
    public const string GymImages = "gym-images";
    public const string Avatars = "avatars";
    /// <summary>Private: served through short-lived signed URLs.</summary>
    public const string OfficialBeta = "official-beta";
    /// <summary>Private: pending and rejected videos must not be reachable by URL.</summary>
    public const string CommunityVideos = "community-videos";
}

/// <summary>
/// Permission for the client to upload one object directly to storage. Binaries never pass through the
/// database and, in production, never through the API either.
/// </summary>
public sealed record UploadTicket(
    string Bucket,
    string Path,
    string UploadUrl,
    string Method,
    IReadOnlyDictionary<string, string> Headers,
    long MaxBytes,
    DateTimeOffset ExpiresAt,
    ResumableUpload? Resumable = null);

/// <summary>
/// Resumable upload over the tus 1.0 protocol: the file is sent in chunks and an interrupted upload continues from the
/// last received byte. Used for videos, which are too large to send reliably in one request on mobile networks.
/// Supabase Storage: POST {url}/storage/v1/upload/resumable/sign with header x-signature = signed upload token,
/// metadata bucketName/objectName/contentType, 6 MB chunks.
/// </summary>
public sealed record ResumableUpload(
    string Endpoint,
    IReadOnlyDictionary<string, string> Headers,
    IReadOnlyDictionary<string, string> Metadata,
    int ChunkSize);

/// <summary>Object storage (Supabase Storage in production, a local folder in development and tests).</summary>
public interface IObjectStorage
{
    Task<UploadTicket> CreateUploadTicketAsync(string bucket, string path, string contentType, long maxBytes, CancellationToken ct = default);

    Task<bool> ExistsAsync(string bucket, string path, CancellationToken ct = default);

    /// <summary>Server-side write, used for seeding.</summary>
    Task PutAsync(string bucket, string path, Stream content, string contentType, CancellationToken ct = default);

    /// <summary>URL a browser can load for an object in a public bucket.</summary>
    string PublicUrl(string bucket, string path);

    /// <summary>Short-lived URL for an object in a PRIVATE bucket. Only issued to viewers who are allowed to see it.</summary>
    Task<string> CreateReadUrlAsync(string bucket, string path, TimeSpan lifetime, CancellationToken ct = default);

    /// <summary>Deletes an object if it exists.</summary>
    Task DeleteAsync(string bucket, string path, CancellationToken ct = default);
}
