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

    private static readonly TimeSpan TicketLifetime = TimeSpan.FromMinutes(30);
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
        return Task.FromResult(new UploadTicket(bucket, path, $"/api/storage/upload/{token}", "PUT",
            new Dictionary<string, string> { ["Content-Type"] = contentType }, maxBytes, expires));
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

    private static string B64(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    private static byte[] FromB64(string s)
    {
        s = s.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(s.PadRight(s.Length + (4 - s.Length % 4) % 4, '='));
    }
}
