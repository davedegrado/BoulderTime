using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BoulderTime.Application.Abstractions;
using Microsoft.Extensions.Configuration;

namespace BoulderTime.Infrastructure.Storage;

/// <summary>
/// Supabase Storage via its REST API, authenticated with the service-role key (server-only).
/// Endpoints (supabase/storage, src/http/routes/object):
///   POST /object/upload/sign/{bucket}/{path} → { url: "/object/upload/sign/...?token=…" } — signed upload URL (valid 2 h)
///   HEAD /object/{bucket}/{path}            — existence check
///   POST /object/{bucket}/{path}            — server-side upload
///   GET  /object/public/{bucket}/{path}     — public read
///   POST /object/sign/{bucket}/{path}        { expiresIn } → { signedURL: "/object/sign/...?token=…" } — private read
///   DELETE /object/{bucket}/{path}           — delete
///   POST/PATCH/HEAD /upload/resumable/sign   — tus resumable upload authorised by header x-signature (src/http/routes/tus)
/// </summary>
public sealed class SupabaseObjectStorage(HttpClient http, IConfiguration configuration, IClock clock) : IObjectStorage
{
    public const string HttpClientName = "supabase-storage";
    private string BaseUrl => $"{configuration["Supabase:Url"]!.TrimEnd('/')}/storage/v1";

    public async Task<UploadTicket> CreateUploadTicketAsync(string bucket, string path, string contentType, long maxBytes, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync($"{BaseUrl}/object/upload/sign/{bucket}/{Encode(path)}", new { }, ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<SignedUpload>(cancellationToken: ct)
                   ?? throw new InvalidOperationException("Supabase Storage returned an empty signed upload response.");
        var token = System.Web.HttpUtility.ParseQueryString(new Uri($"{BaseUrl}{body.Url}").Query)["token"]
                    ?? throw new InvalidOperationException("Supabase Storage returned a signed upload URL without a token.");
        var resumable = new ResumableUpload($"{BaseUrl}/upload/resumable/sign",
            new Dictionary<string, string> { ["x-signature"] = token, ["x-upsert"] = "false" },
            new Dictionary<string, string> { ["bucketName"] = bucket, ["objectName"] = path, ["contentType"] = contentType },
            ChunkSize: 6 * 1024 * 1024); // Supabase requires exactly 6 MB chunks for resumable uploads
        return new UploadTicket(bucket, path, $"{BaseUrl}{body.Url}", "PUT",
            new Dictionary<string, string> { ["Content-Type"] = contentType }, maxBytes, clock.UtcNow.AddHours(2), resumable);
    }

    public async Task<bool> ExistsAsync(string bucket, string path, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Head, $"{BaseUrl}/object/{bucket}/{Encode(path)}");
        using var response = await http.SendAsync(request, ct);
        if (response.IsSuccessStatusCode) return true;
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest) return false;
        response.EnsureSuccessStatusCode();
        return false;
    }

    public async Task PutAsync(string bucket, string path, Stream content, string contentType, CancellationToken ct = default)
    {
        using var body = new StreamContent(content);
        body.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/object/{bucket}/{Encode(path)}") { Content = body };
        request.Headers.Add("x-upsert", "true");
        using var response = await http.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
    }

    public string PublicUrl(string bucket, string path) => $"{BaseUrl}/object/public/{bucket}/{Encode(path)}";

    public async Task<string> CreateReadUrlAsync(string bucket, string path, TimeSpan lifetime, CancellationToken ct = default)
    {
        using var response = await http.PostAsJsonAsync($"{BaseUrl}/object/sign/{bucket}/{Encode(path)}", new { expiresIn = (int)lifetime.TotalSeconds }, ct);
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<SignedRead>(cancellationToken: ct)
                   ?? throw new InvalidOperationException("Supabase Storage returned an empty signed URL response.");
        return $"{BaseUrl}{body.SignedURL}";
    }

    public async Task DeleteAsync(string bucket, string path, CancellationToken ct = default)
    {
        using var response = await http.DeleteAsync($"{BaseUrl}/object/{bucket}/{Encode(path)}", ct);
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.BadRequest) return; // already gone
        response.EnsureSuccessStatusCode();
    }

    private sealed record SignedRead(string SignedURL);

    private static string Encode(string path) => string.Join('/', path.Split('/').Select(Uri.EscapeDataString));

    private sealed record SignedUpload(string Url);
}
