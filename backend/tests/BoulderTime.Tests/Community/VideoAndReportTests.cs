using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Common;
using BoulderTime.Application.Community;
using BoulderTime.Domain.Community;
using BoulderTime.Domain.Staff;
using BoulderTime.Tests.Climbing;
using BoulderTime.Tests.Infrastructure;

namespace BoulderTime.Tests.Community;

[Collection(DatabaseCollection.Name)]
public sealed class VideoAndReportTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private static async Task<List<VideoDto>> VideosAs(HttpClient client, Guid boulderId) =>
        (await (await client.GetAsync($"/api/boulders/{boulderId}/videos")).ReadAsync<List<VideoDto>>())!;

    [Fact]
    public async Task Community_video_is_private_until_approved_and_changes_require_reapproval()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var b = await w.BoulderAsync();
        var author = await _f.UserAsync();
        var climber = await _f.UserAsync();

        var video = await author.SubmitVideoAsync(_f, b.Id);
        video.Status.Should().Be(VideoStatus.Pending);

        (await VideosAs(climber.Client, b.Id)).Should().BeEmpty();
        (await VideosAs(_f.CreateClient(), b.Id)).Should().BeEmpty();
        (await VideosAs(author.Client, b.Id)).Should().ContainSingle(v => v.Id == video.Id && v.IsMine);

        var queue = (await (await w.Staff.Client.GetAsync($"/api/gyms/{w.Gym.Id}/moderation/videos")).ReadAsync<List<ModerationVideoDto>>())!;
        queue.Should().ContainSingle(q => q.Video.Id == video.Id);
        (await climber.Client.GetAsync($"/api/gyms/{w.Gym.Id}/moderation/videos")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await w.Staff.Client.PostAsync($"/api/videos/{video.Id}/approve", null)).EnsureSuccessStatusCode();
        var visible = await VideosAs(climber.Client, b.Id);
        visible.Should().ContainSingle(v => v.Id == video.Id && v.Status == VideoStatus.Approved);
        (await _f.CreateClient().GetAsync(visible[0].VideoUrl)).StatusCode.Should().Be(HttpStatusCode.OK);

        // Modifying an approved video sends it back to review.
        var modified = (await (await author.Client.PatchAsJsonAsync($"/api/videos/{video.Id}", new { caption = "New caption" })).ReadAsync<VideoDto>())!;
        modified.Status.Should().Be(VideoStatus.Pending);
        (await VideosAs(climber.Client, b.Id)).Should().BeEmpty();

        // Deleting needs no approval.
        (await author.Client.DeleteAsync($"/api/videos/{video.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await VideosAs(author.Client, b.Id)).Should().BeEmpty();
    }

    [Fact]
    public async Task Nobody_reviews_their_own_video_and_rejection_needs_a_reason_seen_only_by_the_author()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var b = await w.BoulderAsync();
        var otherStaff = await _f.UserAsync();
        await _f.StaffAsync(w.Gym, otherStaff, GymRole.Staff);

        var own = await w.Staff.SubmitVideoAsync(_f, b.Id);
        (await w.Staff.Client.PostAsync($"/api/videos/{own.Id}/approve", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var author = await _f.UserAsync();
        var video = await author.SubmitVideoAsync(_f, b.Id);
        (await otherStaff.Client.PostAsJsonAsync($"/api/videos/{video.Id}/reject", new { reason = "" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await otherStaff.Client.PostAsJsonAsync($"/api/videos/{video.Id}/reject", new { reason = "Wrong boulder" })).EnsureSuccessStatusCode();

        (await VideosAs(author.Client, b.Id)).Single(v => v.Id == video.Id).RejectionReason.Should().Be("Wrong boulder");
    }

    [Fact]
    public async Task Official_beta_is_staff_only_readable_by_everyone_and_replacing_removes_the_old_file()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var b = await w.BoulderAsync();
        var climber = await _f.UserAsync();

        (await climber.Client.PostAsJsonAsync($"/api/boulders/{b.Id}/video-uploads", new { kind = "BETA", contentType = "video/mp4", sizeBytes = 12 }))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var first = await w.Staff.UploadVideoAsync(_f, b.Id, "BETA");
        (await w.Staff.Client.PutAsJsonAsync($"/api/boulders/{b.Id}/beta", new { storagePath = first, caption = "Official" })).EnsureSuccessStatusCode();
        var beta = (await (await _f.CreateClient().GetAsync($"/api/boulders/{b.Id}/beta")).ReadAsync<BetaDto>())!;
        (await _f.CreateClient().GetAsync(beta.VideoUrl)).StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await w.Staff.UploadVideoAsync(_f, b.Id, "BETA");
        (await w.Staff.Client.PutAsJsonAsync($"/api/boulders/{b.Id}/beta", new { storagePath = second, caption = "Better" })).EnsureSuccessStatusCode();
        File.Exists(Path.Combine(_f.StorageRoot, StorageBuckets.OfficialBeta, first)).Should().BeFalse();
        File.Exists(Path.Combine(_f.StorageRoot, StorageBuckets.OfficialBeta, second)).Should().BeTrue();
    }

    [Fact]
    public async Task Signed_read_urls_cannot_be_forged_and_upload_tickets_are_not_read_urls()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var b = await w.BoulderAsync();
        var author = await _f.UserAsync();
        var video = await author.SubmitVideoAsync(_f, b.Id);
        var anon = _f.CreateClient();

        var tampered = video.VideoUrl[..^3] + (video.VideoUrl.EndsWith("AAA") ? "BBB" : "AAA");
        (await anon.GetAsync(tampered)).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var ticket = (await (await author.Client.PostAsJsonAsync($"/api/boulders/{b.Id}/video-uploads", new { contentType = "video/mp4", sizeBytes = 12 })).ReadAsync<UploadTicket>())!;
        var token = ticket.UploadUrl.Split('/').Last();
        (await anon.GetAsync($"/api/storage/signed/{token}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Reports_are_scoped_to_the_gym_and_resolving_can_remove_the_content()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var other = await ClimbingWorld.CreateAsync(_f, "Other gym");
        var b = await w.BoulderAsync();
        var author = await _f.UserAsync();
        var reporter = await _f.UserAsync();
        var comment = (await (await author.Client.PostAsJsonAsync($"/api/boulders/{b.Id}/comments", new { content = "buy cheap shoes at …" })).ReadAsync<CommentDto>())!;

        (await reporter.Client.PostAsJsonAsync("/api/reports", new { entityType = "COMMENT", entityId = comment.Id, reason = "OTHER" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var report = (await (await reporter.Client.PostAsJsonAsync("/api/reports", new { entityType = "COMMENT", entityId = comment.Id, reason = "SPAM" })).ReadAsync<ReportDto>())!;
        (await reporter.Client.PostAsJsonAsync("/api/reports", new { entityType = "COMMENT", entityId = comment.Id, reason = "SPAM" })).StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Staff of another gym can neither see nor handle it.
        (await other.Staff.Client.GetAsync($"/api/gyms/{w.Gym.Id}/moderation/reports")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await other.Staff.Client.PostAsJsonAsync($"/api/reports/{report.Id}/resolve", new { action = "REMOVE_CONTENT" })).StatusCode.Should().Be(HttpStatusCode.NotFound);

        var summary = (await (await w.Staff.Client.GetAsync($"/api/gyms/{w.Gym.Id}/moderation/summary")).ReadAsync<ModerationSummaryDto>())!;
        summary.PendingReports.Should().Be(1);

        var resolved = (await (await w.Staff.Client.PostAsJsonAsync($"/api/reports/{report.Id}/resolve", new { action = "REMOVE_CONTENT", note = "Spam" })).ReadAsync<ReportDto>())!;
        resolved.Status.Should().Be(ReportStatus.Resolved);
        (await (await _f.CreateClient().GetAsync($"/api/boulders/{b.Id}/comments")).ReadAsync<PagedResult<CommentDto>>())!.Items.Should().BeEmpty();
        (await w.Staff.Client.PostAsJsonAsync($"/api/reports/{report.Id}/dismiss", new { })).StatusCode.Should().Be(HttpStatusCode.Conflict);

        // Platform admins see reports from every gym.
        var admin = await _f.UserAsync(platformAdmin: true);
        var all = (await (await admin.Client.GetAsync("/api/admin/reports?status=RESOLVED")).ReadAsync<PagedResult<ReportDto>>())!;
        all.Items.Should().ContainSingle(r => r.Id == report.Id);
        (await w.Staff.Client.GetAsync("/api/admin/reports")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Users_cannot_report_content_they_cannot_see()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var b = await w.BoulderAsync();
        var author = await _f.UserAsync();
        var snoop = await _f.UserAsync();
        var pending = await author.SubmitVideoAsync(_f, b.Id);

        (await snoop.Client.PostAsJsonAsync("/api/reports", new { entityType = "VIDEO", entityId = pending.Id, reason = "SPAM" })).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
