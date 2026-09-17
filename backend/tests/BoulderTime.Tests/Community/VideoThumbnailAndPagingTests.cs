using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Community;
using BoulderTime.Domain.Community;
using BoulderTime.Tests.Climbing;
using BoulderTime.Tests.Infrastructure;

namespace BoulderTime.Tests.Community;

[Collection(DatabaseCollection.Name)]
public sealed class VideoThumbnailAndPagingTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private static readonly byte[] FakeJpeg = [0xFF, 0xD8, 0xFF, 0xE0, 9, 9, 9];

    private async Task<string> UploadThumbnailAsync(TestUser user, Guid boulderId, string kind = "COMMUNITY_THUMBNAIL")
    {
        var ticket = (await (await user.Client.PostAsJsonAsync($"/api/boulders/{boulderId}/video-uploads", new { kind, contentType = "image/jpeg", sizeBytes = FakeJpeg.Length })).ReadAsync<UploadTicket>())!;
        ticket.Resumable.Should().BeNull(); // small image: single upload
        var body = new ByteArrayContent(FakeJpeg);
        body.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        (await _f.CreateClient().PutAsync(ticket.UploadUrl, body)).EnsureSuccessStatusCode();
        return ticket.Path;
    }

    [Fact]
    public async Task Videos_carry_a_private_thumbnail_that_is_deleted_with_the_video()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var b = await w.BoulderAsync();
        var author = await _f.UserAsync();

        var video = await author.UploadVideoAsync(_f, b.Id);
        var thumb = await UploadThumbnailAsync(author, b.Id);
        var created = (await (await author.Client.PostAsJsonAsync($"/api/boulders/{b.Id}/videos", new { storagePath = video, thumbnailPath = thumb })).ReadAsync<VideoDto>())!;

        created.ThumbnailUrl.Should().NotBeNull();
        (await _f.CreateClient().GetAsync(created.ThumbnailUrl)).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _f.CreateClient().GetAsync($"/api/storage/files/{StorageBuckets.CommunityVideos}/{thumb}")).StatusCode.Should().Be(HttpStatusCode.NotFound); // not public

        (await author.Client.DeleteAsync($"/api/videos/{created.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        File.Exists(Path.Combine(_f.StorageRoot, StorageBuckets.CommunityVideos, thumb)).Should().BeFalse();
    }

    [Fact]
    public async Task Thumbnails_are_optional_but_must_be_uploaded_images_for_the_same_boulder()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var b = await w.BoulderAsync();
        var other = await w.BoulderAsync();
        var author = await _f.UserAsync();

        (await author.Client.PostAsJsonAsync($"/api/boulders/{b.Id}/video-uploads", new { kind = "COMMUNITY_THUMBNAIL", contentType = "image/png", sizeBytes = 10 })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await author.Client.PostAsJsonAsync($"/api/boulders/{b.Id}/video-uploads", new { kind = "COMMUNITY_THUMBNAIL", contentType = "image/jpeg", sizeBytes = 5_000_000 })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var video = await author.UploadVideoAsync(_f, b.Id);
        var foreignThumb = await UploadThumbnailAsync(author, other.Id);
        (await author.Client.PostAsJsonAsync($"/api/boulders/{b.Id}/videos", new { storagePath = video, thumbnailPath = foreignThumb })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var noThumb = (await (await author.Client.PostAsJsonAsync($"/api/boulders/{b.Id}/videos", new { storagePath = video })).ReadAsync<VideoDto>())!;
        noThumb.ThumbnailUrl.Should().BeNull();
    }

    [Fact]
    public async Task Approved_videos_are_paged_and_the_viewers_videos_in_review_come_separately()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var b = await w.BoulderAsync();
        for (var a = 0; a < 5; a++)
        {
            var author = await _f.UserAsync();
            for (var i = 0; i < 3; i++)
            {
                var v = await author.SubmitVideoAsync(_f, b.Id);
                (await w.Staff.Client.PostAsync($"/api/videos/{v.Id}/approve", null)).EnsureSuccessStatusCode();
            }
        }
        var me = await _f.UserAsync();
        var mine = await me.SubmitVideoAsync(_f, b.Id);

        var first = (await (await me.Client.GetAsync($"/api/boulders/{b.Id}/videos")).ReadAsync<BoulderVideosDto>())!;
        first.Approved.Total.Should().Be(15);
        first.Approved.Items.Should().HaveCount(VideoService.VideosPageSize);
        first.Approved.HasMore.Should().BeTrue();
        first.MineInReview.Should().ContainSingle(v => v.Id == mine.Id && v.Status == VideoStatus.Pending);

        var second = (await (await me.Client.GetAsync($"/api/boulders/{b.Id}/videos?page=2")).ReadAsync<BoulderVideosDto>())!;
        second.Approved.Items.Should().HaveCount(3);
        second.MineInReview.Should().BeEmpty();
        first.Approved.Items.Select(v => v.Id).Concat(second.Approved.Items.Select(v => v.Id)).Distinct().Should().HaveCount(15);
    }
}
