using System.Net.Http.Headers;
using System.Net.Http.Json;
using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Community;
using BoulderTime.Tests.Infrastructure;

namespace BoulderTime.Tests.Community;

public static class CommunityHelpers
{
    public static readonly byte[] FakeMp4 = [0, 0, 0, 0x18, 0x66, 0x74, 0x79, 0x70, 1, 2, 3, 4];

    public static async Task<string> UploadVideoAsync(this TestUser user, ApiFactory f, Guid boulderId, string kind = "COMMUNITY")
    {
        var ticketRes = await user.Client.PostAsJsonAsync($"/api/boulders/{boulderId}/video-uploads", new { kind, contentType = "video/mp4", sizeBytes = FakeMp4.Length });
        ticketRes.EnsureSuccessStatusCode();
        var ticket = (await ticketRes.ReadAsync<UploadTicket>())!;
        var body = new ByteArrayContent(FakeMp4);
        body.Headers.ContentType = new MediaTypeHeaderValue("video/mp4");
        (await f.CreateClient().PutAsync(ticket.UploadUrl, body)).EnsureSuccessStatusCode();
        return ticket.Path;
    }

    public static async Task<VideoDto> SubmitVideoAsync(this TestUser user, ApiFactory f, Guid boulderId, string? caption = "My beta")
    {
        var path = await user.UploadVideoAsync(f, boulderId);
        var res = await user.Client.PostAsJsonAsync($"/api/boulders/{boulderId}/videos", new { storagePath = path, caption });
        res.EnsureSuccessStatusCode();
        return (await res.ReadAsync<VideoDto>())!;
    }
}
