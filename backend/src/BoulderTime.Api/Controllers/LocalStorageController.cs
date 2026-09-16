using BoulderTime.Infrastructure.Storage;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;

namespace BoulderTime.Api.Controllers;

/// <summary>
/// Stand-in for Supabase Storage when Storage:Provider is "Local" (development and tests). Returns 404 otherwise.
/// Uploads are authorized by the signed ticket in the URL, exactly like Supabase signed upload URLs.
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
    [RequestSizeLimit(12 * 1024 * 1024)]
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

        // Buffer up to the limit so a missing/incorrect Content-Length can't bypass it.
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await Request.Body.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > ticket.MaxBytes)
                return Problem(statusCode: StatusCodes.Status413PayloadTooLarge, title: "Too large", detail: "The file is too large.");
            buffer.Write(chunk, 0, read);
        }
        if (buffer.Length == 0) return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Invalid request", detail: "The file is empty.");

        buffer.Position = 0;
        await storage.PutAsync(ticket.Bucket, ticket.Path, buffer, ticket.ContentType, ct);
        return Ok(new { ticket.Bucket, ticket.Path });
    }

    [HttpGet("files/{bucket}/{**path}")]
    public IActionResult Read(string bucket, string path)
    {
        if (Storage is not { } storage || !LocalObjectStorage.PublicBuckets.Contains(bucket)) return NotFound();
        var file = storage.Resolve(bucket, path);
        if (file is null || !System.IO.File.Exists(file)) return NotFound();
        if (!ContentTypes.TryGetContentType(file, out var type)) type = "application/octet-stream";
        Response.Headers.CacheControl = "public, max-age=31536000, immutable"; // object paths are unique per upload
        return PhysicalFile(file, type);
    }
}
