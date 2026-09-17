using BoulderTime.Infrastructure.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace BoulderTime.Api.Controllers;

/// <summary>
/// Stand-in for Supabase Storage when Storage:Provider is "Local" (development and tests). Returns 404 otherwise.
/// Uploads are authorized by the signed ticket in the URL, private reads by a signed read URL — like Supabase.
/// </summary>
[ApiController]
[Route("api/storage")]
[AllowAnonymous]
[ApiExplorerSettings(IgnoreApi = true)]
public sealed class LocalStorageController(IServiceProvider services) : ControllerBase
{
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();
    private LocalObjectStorage? Storage => services.GetService<LocalObjectStorage>();

    [HttpPut("upload/{token}")]
    [RequestSizeLimit(110 * 1024 * 1024)]
    public async Task<IActionResult> Upload(string token, CancellationToken ct)
    {
        if (Storage is not { } storage) return NotFound();
        var ticket = storage.ReadTicket(token);
        if (ticket is null) return Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: "This upload link is invalid or has expired.");

        var contentType = Request.ContentType?.Split(';')[0].Trim();
        if (!string.Equals(contentType, ticket.ContentType, StringComparison.OrdinalIgnoreCase))
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid request", detail: $"Expected content type {ticket.ContentType}.");
        if (Request.ContentLength is { } length && length > ticket.MaxBytes)
            return Problem(statusCode: StatusCodes.Status413PayloadTooLarge, title: "Too large", detail: "The file is too large.");

        // Stream to a temp file with a hard byte limit (videos can be ~100 MB; never buffer in memory).
        var temp = Path.GetTempFileName();
        try
        {
            long written = 0;
            await using (var output = System.IO.File.Create(temp))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = await Request.Body.ReadAsync(buffer, ct)) > 0)
                {
                    written += read;
                    if (written > ticket.MaxBytes)
                        return Problem(statusCode: StatusCodes.Status413PayloadTooLarge, title: "Too large", detail: "The file is too large.");
                    await output.WriteAsync(buffer.AsMemory(0, read), ct);
                }
            }
            if (written == 0) return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid request", detail: "The file is empty.");

            await using var input = System.IO.File.OpenRead(temp);
            await storage.PutAsync(ticket.Bucket, ticket.Path, input, ticket.ContentType, ct);
            return Ok(new { ticket.Bucket, ticket.Path });
        }
        finally
        {
            System.IO.File.Delete(temp);
        }
    }

    // ---------- tus 1.0 resumable uploads (creation + core), mirroring Supabase's /upload/resumable/sign ----------

    private const string TusVersion = "1.0.0";

    [HttpPost("tus")]
    public async Task<IActionResult> TusCreate(CancellationToken ct)
    {
        if (Storage is not { } storage) return NotFound();
        Response.Headers["Tus-Resumable"] = TusVersion;
        var token = Request.Headers["x-signature"].ToString();
        var ticket = storage.ReadTicket(token);
        if (ticket is null) return Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: "This upload link is invalid or has expired.");
        if (!long.TryParse(Request.Headers["Upload-Length"], out var length) || length <= 0)
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid request", detail: "Upload-Length is required.");
        if (length > ticket.MaxBytes)
            return Problem(statusCode: StatusCodes.Status413PayloadTooLarge, title: "Too large", detail: "The file is too large.");

        var metadata = ParseMetadata(Request.Headers["Upload-Metadata"].ToString());
        if (metadata.GetValueOrDefault("bucketName") != ticket.Bucket || metadata.GetValueOrDefault("objectName") != ticket.Path
            || !string.Equals(metadata.GetValueOrDefault("contentType"), ticket.ContentType, StringComparison.OrdinalIgnoreCase))
            return Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: "The upload doesn't match its link.");

        var state = await storage.CreateTusAsync(ticket, token, length, ct);
        Response.Headers.Location = $"/api/storage/tus/{state.Id}";
        return StatusCode(StatusCodes.Status201Created);
    }

    [HttpHead("tus/{id}")]
    public async Task<IActionResult> TusHead(string id, CancellationToken ct)
    {
        if (Storage is not { } storage) return NotFound();
        Response.Headers["Tus-Resumable"] = TusVersion;
        Response.Headers.CacheControl = "no-store";
        var state = await storage.FindTusAsync(id, Request.Headers["x-signature"].ToString(), ct);
        if (state is null) return NotFound();
        Response.Headers["Upload-Offset"] = state.Offset.ToString();
        Response.Headers["Upload-Length"] = state.Length.ToString();
        return Ok();
    }

    [HttpPatch("tus/{id}")]
    [RequestSizeLimit(LocalObjectStorage.ResumableChunkSize + 1024 * 1024)]
    public async Task<IActionResult> TusPatch(string id, CancellationToken ct)
    {
        if (Storage is not { } storage) return NotFound();
        Response.Headers["Tus-Resumable"] = TusVersion;
        var token = Request.Headers["x-signature"].ToString();
        if (storage.ReadTicket(token) is null) return Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: "This upload link is invalid or has expired.");
        var state = await storage.FindTusAsync(id, token, ct);
        if (state is null) return NotFound();
        if (!string.Equals(Request.ContentType, "application/offset+octet-stream", StringComparison.OrdinalIgnoreCase))
            return StatusCode(StatusCodes.Status415UnsupportedMediaType);
        if (!long.TryParse(Request.Headers["Upload-Offset"], out var offset))
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid request", detail: "Upload-Offset is required.");

        var (result, next) = await storage.AppendTusAsync(state, offset, Request.Body, LocalObjectStorage.ResumableChunkSize + 1024 * 1024, ct);
        Response.Headers["Upload-Offset"] = next.Offset.ToString();
        return result switch
        {
            LocalObjectStorage.TusAppendResult.OffsetMismatch => StatusCode(StatusCodes.Status409Conflict),
            LocalObjectStorage.TusAppendResult.TooLarge => StatusCode(StatusCodes.Status413PayloadTooLarge),
            _ => NoContent(),
        };
    }

    /// <summary>tus Upload-Metadata: comma-separated "key base64value" pairs.</summary>
    private static Dictionary<string, string> ParseMetadata(string header)
    {
        var result = new Dictionary<string, string>();
        foreach (var pair in header.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = pair.Split(' ', 2);
            try { result[parts[0]] = parts.Length == 2 ? System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(parts[1])) : ""; }
            catch (FormatException) { /* ignore malformed pair */ }
        }
        return result;
    }

    [HttpGet("files/{bucket}/{**path}")]
    public IActionResult Read(string bucket, string path)
    {
        if (Storage is not { } storage || !LocalObjectStorage.PublicBuckets.Contains(bucket)) return NotFound();
        return Serve(storage.Resolve(bucket, path), "public, max-age=31536000, immutable");
    }

    /// <summary>Private objects (videos) through an expiring signed URL. Supports range requests for video seeking.</summary>
    [HttpGet("signed/{token}")]
    public IActionResult ReadSigned(string token)
    {
        if (Storage is not { } storage) return NotFound();
        var read = storage.ReadSignedUrl(token);
        if (read is null) return Problem(statusCode: StatusCodes.Status403Forbidden, title: "Forbidden", detail: "This link is invalid or has expired.");
        return Serve(storage.Resolve(read.Bucket, read.Path), "private, max-age=600");
    }

    private IActionResult Serve(string? file, string cacheControl)
    {
        if (file is null || !System.IO.File.Exists(file)) return NotFound();
        if (!ContentTypes.TryGetContentType(file, out var type)) type = "application/octet-stream";
        Response.Headers.CacheControl = cacheControl;
        return PhysicalFile(file, type, enableRangeProcessing: true);
    }
}
