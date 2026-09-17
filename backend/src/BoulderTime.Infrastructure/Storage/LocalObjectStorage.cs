using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BoulderTime.Application.Abstractions;
using Microsoft.Extensions.Options;

namespace BoulderTime.Infrastructure.Storage;

/// <summary>
/// Development/test storage on the local disk that behaves like Supabase Storage from the client's point of view:
/// the API issues a signed, expiring upload URL and the browser PUTs the file to it.
/// </summary>
public sealed class LocalObjectStorage : IObjectStorage
{
    public static readonly IReadOnlySet<string> PublicBuckets = new HashSet<string>
    {
        StorageBuckets.BoulderImages, StorageBuckets.GymImages, StorageBuckets.Avatars,
    };

    private static readonly TimeSpan TicketLifetime = TimeSpan.FromHours(2); // same as Supabase signed upload URLs
    public const int ResumableChunkSize = 6 * 1024 * 1024;
    private readonly string _root;
    private readonly byte[] _key;
    private readonly IClock _clock;

    public LocalObjectStorage(IOptions<StorageOptions> options, IClock clock)
    {
        _root = Path.GetFullPath(options.Value.Local.RootPath);
        _key = string.IsNullOrWhiteSpace(options.Value.Local.SigningKey)
            ? RandomNumberGenerator.GetBytes(32)
            : Encoding.UTF8.GetBytes(options.Value.Local.SigningKey);
        _clock = clock;
    }

    public sealed record TicketPayload(string Bucket, string Path, string ContentType, long MaxBytes, long ExpiresUnix);
    public sealed record ReadPayload(string Bucket, string Path, long ExpiresUnix, string Purpose);

    public Task<UploadTicket> CreateUploadTicketAsync(string bucket, string path, string contentType, long maxBytes, CancellationToken ct = default)
    {
        var expires = _clock.UtcNow + TicketLifetime;
        var payload = JsonSerializer.SerializeToUtf8Bytes(new TicketPayload(bucket, path, contentType, maxBytes, expires.ToUnixTimeSeconds()));
        var token = $"{B64(payload)}.{B64(HMACSHA256.HashData(_key, payload))}";
        var resumable = new ResumableUpload("/api/storage/tus",
            new Dictionary<string, string> { ["x-signature"] = token },
            new Dictionary<string, string> { ["bucketName"] = bucket, ["objectName"] = path, ["contentType"] = contentType },
            ResumableChunkSize);
        return Task.FromResult(new UploadTicket(bucket, path, $"/api/storage/upload/{token}", "PUT",
            new Dictionary<string, string> { ["Content-Type"] = contentType }, maxBytes, expires, resumable));
    }

    /// <summary>Verifies signature and expiry. Returns null for any invalid token.</summary>
    public TicketPayload? ReadTicket(string token)
    {
        var payload = Verify(token);
        if (payload is null) return null;
        try
        {
            using var doc = JsonDocument.Parse(payload);
            if (doc.RootElement.TryGetProperty("Purpose", out _)) return null; // a read URL is not an upload ticket
            var ticket = JsonSerializer.Deserialize<TicketPayload>(payload);
            return ticket is not null && ticket.ExpiresUnix > _clock.UtcNow.ToUnixTimeSeconds() ? ticket : null;
        }
        catch (JsonException) { return null; }
    }

    private byte[]? Verify(string token)
    {
        var parts = token.Split('.');
        if (parts.Length != 2) return null;
        try
        {
            var payload = FromB64(parts[0]);
            return CryptographicOperations.FixedTimeEquals(HMACSHA256.HashData(_key, payload), FromB64(parts[1])) ? payload : null;
        }
        catch (FormatException) { return null; }
    }

    public Task<string> CreateReadUrlAsync(string bucket, string path, TimeSpan lifetime, CancellationToken ct = default)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new ReadPayload(bucket, path, (_clock.UtcNow + lifetime).ToUnixTimeSeconds(), "read"));
        return Task.FromResult($"/api/storage/signed/{B64(payload)}.{B64(HMACSHA256.HashData(_key, payload))}");
    }

    public ReadPayload? ReadSignedUrl(string token)
    {
        var payload = Verify(token);
        if (payload is null) return null;
        try
        {
            // Require the purpose to be explicitly present, so an upload ticket can never act as a read URL.
            using var doc = JsonDocument.Parse(payload);
            if (!doc.RootElement.TryGetProperty("Purpose", out var purpose) || purpose.GetString() != "read") return null;
            var read = JsonSerializer.Deserialize<ReadPayload>(payload);
            return read is not null && read.ExpiresUnix > _clock.UtcNow.ToUnixTimeSeconds() ? read : null;
        }
        catch (JsonException) { return null; }
    }

    public Task DeleteAsync(string bucket, string path, CancellationToken ct = default)
    {
        if (Resolve(bucket, path) is { } file && File.Exists(file)) File.Delete(file);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(string bucket, string path, CancellationToken ct = default) =>
        Task.FromResult(Resolve(bucket, path) is { } file && File.Exists(file));

    public async Task PutAsync(string bucket, string path, Stream content, string contentType, CancellationToken ct = default)
    {
        var file = Resolve(bucket, path) ?? throw new ArgumentException("Invalid object path.", nameof(path));
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        await using var output = File.Create(file);
        await content.CopyToAsync(output, ct);
    }

    public string PublicUrl(string bucket, string path) =>
        $"/api/storage/files/{bucket}/{string.Join('/', path.Split('/').Select(Uri.EscapeDataString))}";

    /// <summary>Absolute file path for an object, or null if the path would escape the bucket folder.</summary>
    public string? Resolve(string bucket, string path)
    {
        if (string.IsNullOrWhiteSpace(bucket) || string.IsNullOrWhiteSpace(path) || bucket.Contains('/') || bucket.Contains("..")) return null;
        var bucketRoot = Path.GetFullPath(Path.Combine(_root, bucket)) + Path.DirectorySeparatorChar;
        var full = Path.GetFullPath(Path.Combine(bucketRoot, path));
        return full.StartsWith(bucketRoot, StringComparison.Ordinal) ? full : null;
    }

    // ---------- tus resumable uploads (development stand-in for Supabase's /upload/resumable) ----------

    public sealed record TusState(string Id, string Bucket, string Path, string ContentType, long Length, long Offset, string TokenHash);

    private static readonly System.Collections.Concurrent.ConcurrentDictionary<string, SemaphoreSlim> TusLocks = new();
    private string TusDir => Path.Combine(_root, ".tus");
    private static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public async Task<TusState> CreateTusAsync(TicketPayload ticket, string token, long length, CancellationToken ct)
    {
        Directory.CreateDirectory(TusDir);
        var state = new TusState(Guid.NewGuid().ToString("N"), ticket.Bucket, ticket.Path, ticket.ContentType, length, 0, Hash(token));
        await File.WriteAllBytesAsync(Path.Combine(TusDir, state.Id + ".bin"), [], ct);
        await SaveTusAsync(state, ct);
        return state;
    }

    /// <summary>Loads an upload if it exists and the caller presents the same signed token that created it.</summary>
    public async Task<TusState?> FindTusAsync(string id, string? token, CancellationToken ct)
    {
        if (token is null || id.Length != 32 || !id.All(Uri.IsHexDigit)) return null;
        var file = Path.Combine(TusDir, id + ".json");
        if (!File.Exists(file)) return null;
        var state = JsonSerializer.Deserialize<TusState>(await File.ReadAllBytesAsync(file, ct));
        return state is not null && CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(state.TokenHash), Encoding.UTF8.GetBytes(Hash(token))) ? state : null;
    }

    public enum TusAppendResult { Ok, OffsetMismatch, TooLarge }

    /// <summary>Appends one chunk at the expected offset. When the last byte arrives, the object is stored and temp files removed.</summary>
    public async Task<(TusAppendResult Result, TusState State)> AppendTusAsync(TusState state, long offset, Stream body, long maxChunk, CancellationToken ct)
    {
        var gate = TusLocks.GetOrAdd(state.Id, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            var current = await FindStateUnlockedAsync(state.Id, ct) ?? state;
            if (offset != current.Offset) return (TusAppendResult.OffsetMismatch, current);

            var data = Path.Combine(TusDir, state.Id + ".bin");
            long written = 0;
            await using (var output = new FileStream(data, FileMode.Append, FileAccess.Write))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = await body.ReadAsync(buffer, ct)) > 0)
                {
                    written += read;
                    if (written > maxChunk || current.Offset + written > current.Length)
                    {
                        output.SetLength(current.Offset); // drop the partial chunk
                        return (TusAppendResult.TooLarge, current);
                    }
                    await output.WriteAsync(buffer.AsMemory(0, read), ct);
                }
            }

            var next = current with { Offset = current.Offset + written };
            if (next.Offset == next.Length)
            {
                await using (var input = File.OpenRead(data)) await PutAsync(next.Bucket, next.Path, input, next.ContentType, ct);
                File.Delete(data);
                File.Delete(Path.Combine(TusDir, state.Id + ".json"));
                TusLocks.TryRemove(state.Id, out _);
            }
            else
            {
                await SaveTusAsync(next, ct);
            }
            return (TusAppendResult.Ok, next);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<TusState?> FindStateUnlockedAsync(string id, CancellationToken ct)
    {
        var file = Path.Combine(TusDir, id + ".json");
        return File.Exists(file) ? JsonSerializer.Deserialize<TusState>(await File.ReadAllBytesAsync(file, ct)) : null;
    }

    private Task SaveTusAsync(TusState state, CancellationToken ct) =>
        File.WriteAllBytesAsync(Path.Combine(TusDir, state.Id + ".json"), JsonSerializer.SerializeToUtf8Bytes(state), ct);

    private static string B64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static byte[] FromB64(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
    }
}
