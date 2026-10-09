using System.Net;
using System.Net.Http.Json;
using BoulderTime.Application.Community;
using BoulderTime.Application.Gyms;
using BoulderTime.Tests.Climbing;
using BoulderTime.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Tests.Community;

/// <summary>
/// An official beta that points at a video already published elsewhere. Gyms film for Instagram anyway, so linking
/// costs them no second upload — and costs us no storage, which is why a link is never refused by the allowance.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class BetaLinkTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    [Theory]
    [InlineData("https://www.youtube.com/watch?v=abc123&list=xyz", "https://www.youtube.com/watch?v=abc123&list=xyz")]
    [InlineData("youtu.be/abc123", "https://youtu.be/abc123")]           // the scheme people leave out
    [InlineData("  instagram.com/reel/XYZ/  ", "https://instagram.com/reel/XYZ/")]
    [InlineData("https://m.youtube.com/watch?v=q", "https://m.youtube.com/watch?v=q")]  // a real subdomain
    [InlineData("https://youtu.be/abc#t=30", "https://youtu.be/abc")]    // the fragment is ours to drop
    public void An_address_we_accept_is_stored_as_it_will_be_opened(string pasted, string stored) =>
        BetaLink.Normalise(pasted).Should().Be(stored);

    [Theory]
    [InlineData("http://youtube.com/watch?v=a")]                  // plain http: handed to every climber's browser
    [InlineData("https://youtube.com.example.org/watch?v=a")]      // a look-alike host, not YouTube
    [InlineData("https://youtube.com/")]                           // the front page is not a beta
    [InlineData("javascript:alert(1)")]
    [InlineData("https://evil.com/video")]
    public void Anything_else_is_refused(string pasted) => BetaLink.Normalise(pasted).Should().BeNull();

    [Fact]
    public void An_empty_address_is_refused_rather_than_throwing()
    {
        BetaLink.Normalise(null).Should().BeNull();
        BetaLink.Normalise("").Should().BeNull();
        BetaLink.Normalise("   ").Should().BeNull();
    }

    [Fact]
    public async Task Staff_can_publish_a_link_instead_of_a_video_and_everyone_sees_it()
    {
        var world = await ClimbingWorld.CreateAsync(_f);
        var boulder = await world.BoulderAsync("6A");

        var saved = await (await world.Staff.Client.PutAsJsonAsync($"/api/boulders/{boulder.Id}/beta",
            new { externalUrl = "instagram.com/reel/ABC/", caption = "Dal nostro profilo" })).ReadAsync<BetaDto>();

        saved!.ExternalUrl.Should().Be("https://instagram.com/reel/ABC/");
        // There is no file of ours, so there is no signed address to hand out either.
        saved.VideoUrl.Should().BeEmpty();
        saved.ThumbnailUrl.Should().BeNull();

        var seenByVisitor = (await (await _f.CreateClient().GetAsync($"/api/boulders/{boulder.Id}/beta")).ReadAsync<BetaDto>())!;
        seenByVisitor.ExternalUrl.Should().Be("https://instagram.com/reel/ABC/");
        seenByVisitor.Caption.Should().Be("Dal nostro profilo");

        // The beta is the gym's: who on the staff published it is for the staff to know, not for climbers.
        seenByVisitor.UploadedBy.Should().BeNull();
        var climber = await _f.UserAsync();
        (await (await climber.Client.GetAsync($"/api/boulders/{boulder.Id}/beta")).ReadAsync<BetaDto>())!.UploadedBy.Should().BeNull();
        var seenByStaff = (await (await world.Staff.Client.GetAsync($"/api/boulders/{boulder.Id}/beta")).ReadAsync<BetaDto>())!;
        seenByStaff.UploadedBy!.UserId.Should().Be(world.Staff.Id);
    }

    [Fact]
    public async Task A_refused_address_is_a_validation_error_and_nothing_is_saved()
    {
        var world = await ClimbingWorld.CreateAsync(_f);
        var boulder = await world.BoulderAsync("6A");

        var refused = await world.Staff.Client.PutAsJsonAsync($"/api/boulders/{boulder.Id}/beta",
            new { externalUrl = "https://evil.example.com/watch" });

        refused.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await _f.Db(db => db.BoulderBetas.CountAsync())).Should().Be(0);
    }

    [Fact]
    public async Task A_link_is_allowed_even_when_the_gym_has_run_out_of_uploads()
    {
        var world = await ClimbingWorld.CreateAsync(_f);
        var admin = await _f.UserAsync(platformAdmin: true);
        var first = await world.BoulderAsync("6A");
        var second = await world.BoulderAsync("6B");
        await admin.Client.PutAsJsonAsync($"/api/admin/gyms/{world.Gym.Id}/video-allowance",
            new { communityVideosEnabled = false, officialBetaLimit = 1 });

        var path = await world.Staff.UploadVideoAsync(_f, first.Id, "BETA");
        (await world.Staff.Client.PutAsJsonAsync($"/api/boulders/{first.Id}/beta", new { storagePath = path })).EnsureSuccessStatusCode();

        // The allowance is about what we store, so a link gets through where a second upload would not.
        (await world.Staff.Client.PostAsJsonAsync($"/api/boulders/{second.Id}/video-uploads",
            new { kind = "BETA", contentType = "video/mp4", sizeBytes = 2_000_000 })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await world.Staff.Client.PutAsJsonAsync($"/api/boulders/{second.Id}/beta",
            new { externalUrl = "https://youtu.be/second" })).EnsureSuccessStatusCode();

        // And it does not eat into the allowance afterwards: still one upload used, of one.
        var allowance = (await (await admin.Client.GetAsync($"/api/admin/gyms/{world.Gym.Id}/video-allowance")).ReadAsync<VideoAllowanceDto>())!;
        allowance.OfficialBetaUsed.Should().Be(1);
    }

    [Fact]
    public async Task Swapping_an_upload_for_a_link_gives_the_gym_its_slot_back()
    {
        var world = await ClimbingWorld.CreateAsync(_f);
        var admin = await _f.UserAsync(platformAdmin: true);
        var boulder = await world.BoulderAsync("6A");
        await admin.Client.PutAsJsonAsync($"/api/admin/gyms/{world.Gym.Id}/video-allowance",
            new { communityVideosEnabled = false, officialBetaLimit = 1 });
        var path = await world.Staff.UploadVideoAsync(_f, boulder.Id, "BETA");
        (await world.Staff.Client.PutAsJsonAsync($"/api/boulders/{boulder.Id}/beta", new { storagePath = path })).EnsureSuccessStatusCode();

        (await world.Staff.Client.PutAsJsonAsync($"/api/boulders/{boulder.Id}/beta",
            new { externalUrl = "https://youtu.be/instead" })).EnsureSuccessStatusCode();

        // The video we were keeping is gone from storage, and the row no longer points at it.
        var beta = await _f.Db(db => db.BoulderBetas.AsNoTracking().FirstAsync());
        beta.StoragePath.Should().BeEmpty();
        beta.ExternalUrl.Should().Be("https://youtu.be/instead");
        var allowance = (await (await admin.Client.GetAsync($"/api/admin/gyms/{world.Gym.Id}/video-allowance")).ReadAsync<VideoAllowanceDto>())!;
        allowance.OfficialBetaUsed.Should().Be(0);
    }

    [Fact]
    public async Task A_link_can_be_replaced_by_an_upload_and_only_staff_may_do_either()
    {
        var world = await ClimbingWorld.CreateAsync(_f);
        var boulder = await world.BoulderAsync("6A");
        var climber = await _f.UserAsync();

        (await climber.Client.PutAsJsonAsync($"/api/boulders/{boulder.Id}/beta",
            new { externalUrl = "https://youtu.be/abc" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        (await world.Staff.Client.PutAsJsonAsync($"/api/boulders/{boulder.Id}/beta",
            new { externalUrl = "https://youtu.be/abc" })).EnsureSuccessStatusCode();
        var path = await world.Staff.UploadVideoAsync(_f, boulder.Id, "BETA");
        var uploaded = (await (await world.Staff.Client.PutAsJsonAsync($"/api/boulders/{boulder.Id}/beta",
            new { storagePath = path })).ReadAsync<BetaDto>())!;

        // Back to a video of ours: the link is cleared, or both would claim to be the beta.
        uploaded.ExternalUrl.Should().BeNull();
        uploaded.VideoUrl.Should().NotBeEmpty();
        (await _f.Db(db => db.BoulderBetas.CountAsync())).Should().Be(1);
    }
}
