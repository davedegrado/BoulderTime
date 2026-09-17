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
