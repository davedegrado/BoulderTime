using System.Net;
using System.Net.Http.Json;
using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Community;
using BoulderTime.Tests.Climbing;
using BoulderTime.Tests.Infrastructure;

namespace BoulderTime.Tests.Community;

/// <summary>The server-wide switch for video uploads (ADR-049): off, no file is taken; linking a beta still works.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class VideoUploadSwitchTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _off = new(postgres) { VideoUploads = false };
    public Task InitializeAsync() => _off.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _off.DisposeAsync();

    [Fact]
    public async Task With_uploads_off_no_video_file_is_accepted_but_a_beta_can_still_be_linked()
    {
        var w = await ClimbingWorld.CreateAsync(_off);
        var b = await w.BoulderAsync();
        var climber = await _off.UserAsync();

        foreach (var (who, kind) in new[] { (climber, "COMMUNITY"), (w.Staff, "BETA"), (w.Staff, "BETA_THUMBNAIL"), (climber, "COMMUNITY_THUMBNAIL") })
        {
            var contentType = kind.EndsWith("THUMBNAIL") ? "image/jpeg" : "video/mp4";
            (await who.Client.PostAsJsonAsync($"/api/boulders/{b.Id}/video-uploads", new { kind, contentType, sizeBytes = 1000 }))
                .StatusCode.Should().Be(HttpStatusCode.Conflict);
        }

        (await w.Staff.Client.PutAsJsonAsync($"/api/boulders/{b.Id}/beta", new { externalUrl = "https://youtu.be/abc" })).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task The_app_is_told_whether_uploads_are_on_and_the_limit()
    {
        var off = (await (await _off.CreateClient().GetAsync("/api/settings/videos")).ReadAsync<VideoSettingsDto>())!;
        off.UploadsEnabled.Should().BeFalse();
        off.MaxUploadMegabytes.Should().Be(50);

        await using var on = new ApiFactory(postgres);
        var settings = (await (await on.CreateClient().GetAsync("/api/settings/videos")).ReadAsync<VideoSettingsDto>())!;
        settings.UploadsEnabled.Should().BeTrue();
    }

    [Fact]
    public async Task A_video_over_the_limit_is_refused_before_any_upload()
    {
        await using var on = new ApiFactory(postgres);
        var w = await ClimbingWorld.CreateAsync(on);
        var b = await w.BoulderAsync();
        var res = await w.Staff.Client.PostAsJsonAsync($"/api/boulders/{b.Id}/video-uploads", new { kind = "BETA", contentType = "video/mp4", sizeBytes = 51L * 1024 * 1024 });
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await res.Content.ReadAsStringAsync()).Should().Contain("Videos must be under 50 MB.");
    }
}
