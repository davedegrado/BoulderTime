using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Community;
using BoulderTime.Domain.Community;
using BoulderTime.Tests.Climbing;
using BoulderTime.Tests.Infrastructure;

namespace BoulderTime.Tests.Community;

/// <summary>The tus flow the browser uses for videos: create, send chunks, resume, finish — then the video is submittable.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class ResumableUploadTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private static string Metadata(IReadOnlyDictionary<string, string> m) =>
        string.Join(",", m.Select(kv => $"{kv.Key} {Convert.ToBase64String(Encoding.UTF8.GetBytes(kv.Value))}"));

    private static HttpRequestMessage Tus(HttpMethod method, string url, string signature)
    {
        var req = new HttpRequestMessage(method, url);
        req.Headers.Add("Tus-Resumable", "1.0.0");
        req.Headers.Add("x-signature", signature);
        return req;
    }

    private static HttpRequestMessage Chunk(string url, string signature, long offset, byte[] data)
    {
        var req = Tus(HttpMethod.Patch, url, signature);
        req.Headers.Add("Upload-Offset", offset.ToString());
        req.Content = new ByteArrayContent(data);
        req.Content.Headers.ContentType = new MediaTypeHeaderValue("application/offset+octet-stream");
        return req;
    }

    private async Task<(UploadTicket Ticket, TestUser User, Guid BoulderId)> TicketAsync(long size)
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var b = await w.BoulderAsync();
        var user = await _f.UserAsync();
        var res = await user.Client.PostAsJsonAsync($"/api/boulders/{b.Id}/video-uploads", new { contentType = "video/quicktime", sizeBytes = size });
        res.EnsureSuccessStatusCode();
        return ((await res.ReadAsync<UploadTicket>())!, user, b.Id);
    }

    [Fact]
    public async Task A_video_uploads_in_chunks_resumes_after_an_interruption_and_can_then_be_submitted()
    {
        var file = Enumerable.Range(0, 3000).Select(i => (byte)(i % 251)).ToArray();
        var (ticket, user, boulderId) = await TicketAsync(file.Length);
        var tus = ticket.Resumable!;
        tus.ChunkSize.Should().Be(6 * 1024 * 1024);
        var sig = tus.Headers["x-signature"];
        var http = _f.CreateClient();

        var create = Tus(HttpMethod.Post, tus.Endpoint, sig);
        create.Headers.Add("Upload-Length", file.Length.ToString());
        create.Headers.Add("Upload-Metadata", Metadata(tus.Metadata));
        var created = await http.SendAsync(create);
        created.StatusCode.Should().Be(HttpStatusCode.Created);
        var location = created.Headers.Location!.ToString();

        // First chunk arrives.
        var first = await http.SendAsync(Chunk(location, sig, 0, file[..1000]));
        first.StatusCode.Should().Be(HttpStatusCode.NoContent);
        first.Headers.GetValues("Upload-Offset").Single().Should().Be("1000");

        // Connection drops; the client asks where to resume.
        var head = await http.SendAsync(Tus(HttpMethod.Head, location, sig));
        head.Headers.GetValues("Upload-Offset").Single().Should().Be("1000");
        head.Headers.GetValues("Upload-Length").Single().Should().Be("3000");

        // A chunk sent at the wrong offset is refused without corrupting the file.
        (await http.SendAsync(Chunk(location, sig, 0, file[..1000]))).StatusCode.Should().Be(HttpStatusCode.Conflict);

        (await http.SendAsync(Chunk(location, sig, 1000, file[1000..2000]))).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var last = await http.SendAsync(Chunk(location, sig, 2000, file[2000..]));
        last.Headers.GetValues("Upload-Offset").Single().Should().Be("3000");

        File.ReadAllBytes(Path.Combine(_f.StorageRoot, StorageBuckets.CommunityVideos, ticket.Path)).Should().Equal(file);
        var submitted = await user.Client.PostAsJsonAsync($"/api/boulders/{boulderId}/videos", new { storagePath = ticket.Path });
        submitted.StatusCode.Should().Be(HttpStatusCode.Created);
        (await submitted.ReadAsync<VideoDto>())!.Status.Should().Be(VideoStatus.Pending);
    }

    [Fact]
    public async Task Resumable_uploads_require_the_signed_token_and_matching_metadata_and_size()
    {
        var (ticket, _, _) = await TicketAsync(100);
        var tus = ticket.Resumable!;
        var sig = tus.Headers["x-signature"];
        var http = _f.CreateClient();

        HttpRequestMessage Create(string signature, long length, IReadOnlyDictionary<string, string> metadata)
        {
            var r = Tus(HttpMethod.Post, tus.Endpoint, signature);
            r.Headers.Add("Upload-Length", length.ToString());
            r.Headers.Add("Upload-Metadata", Metadata(metadata));
            return r;
        }

        (await http.SendAsync(Create(sig[..^3] + "AAA", 100, tus.Metadata))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var otherPath = new Dictionary<string, string>(tus.Metadata) { ["objectName"] = "gyms/x/boulders/y/videos/evil.mp4" };
        (await http.SendAsync(Create(sig, 100, otherPath))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await http.SendAsync(Create(sig, 200 * 1024 * 1024, tus.Metadata))).StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);

        var created = await http.SendAsync(Create(sig, 100, tus.Metadata));
        var location = created.Headers.Location!.ToString();
        (await http.SendAsync(Tus(HttpMethod.Head, location, "not-the-token"))).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await http.SendAsync(Chunk(location, sig, 0, new byte[150]))).StatusCode.Should().Be(HttpStatusCode.RequestEntityTooLarge);
    }
}
