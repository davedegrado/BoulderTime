using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Boulders;
using BoulderTime.Tests.Climbing;
using BoulderTime.Tests.Infrastructure;

namespace BoulderTime.Tests.Boulders;

/// <summary>
/// A highlighted photo keeps what it was made from — the original photo, the mask, the choices — so staff go on
/// from where they left it. The files are the boulder's (or, for a wall photo, the sector's) and are cleaned up.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class HighlightTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private const string Settings = """{"color":"YELLOW","outline":{"on":true,"color":"red","level":2}}""";
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 1, 2, 3];

    private async Task<string> UploadAsync(TestUser staff, string url, object body, byte[] bytes, string type)
    {
        var res = await staff.Client.PostAsJsonAsync(url, body);
        res.EnsureSuccessStatusCode();
        var ticket = (await res.ReadAsync<UploadTicket>())!;
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue(type);
        (await _f.CreateClient().PutAsync(ticket.UploadUrl, content)).EnsureSuccessStatusCode();
        return ticket.Path;
    }
    private Task<string> MaskAsync(ClimbingWorld w) =>
        UploadAsync(w.Staff, $"/api/gyms/{w.Gym.Id}/boulder-photos", new { contentType = "image/png", sizeBytes = Png.Length, mask = true }, Png, "image/png");
    private Task<string> WallAsync(ClimbingWorld w) =>
        UploadAsync(w.Staff, $"/api/sectors/{w.Sector.Id}/photo-uploads", new { contentType = "image/jpeg", sizeBytes = BoulderTestData.FakeJpeg.Length }, BoulderTestData.FakeJpeg, "image/jpeg");
    private bool Stored(string path) => File.Exists(Path.Combine(_f.StorageRoot, StorageBuckets.BoulderImages, path));

    private object Body(ClimbingWorld w, string photo, object? highlight) => new
    {
        sectorId = w.Sector.Id, photoPath = photo, holdColor = "YELLOW", grades = Array.Empty<object>(), highlight,
    };

    private async Task<BoulderDetailDto> DetailAsync(HttpClient client, Guid id) =>
        (await (await client.GetAsync($"/api/boulders/{id}")).ReadAsync<BoulderDetailDto>())!;

    [Fact]
    public async Task A_highlight_is_kept_with_the_boulder_for_its_staff_to_go_on_editing()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var original = await w.Staff.UploadPhotoAsync(_f, w.Gym);
        var result = await w.Staff.UploadPhotoAsync(_f, w.Gym);
        var mask = await MaskAsync(w);
        var res = await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders", Body(w, result, new { sourcePath = original, maskPath = mask, settings = Settings }));
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        var created = (await res.ReadAsync<BoulderDetailDto>())!;

        created.Highlight.Should().Be(new BoulderHighlightDto(original, mask, Settings));
        (await DetailAsync(_f.CreateClient(), created.Id)).Highlight.Should().BeNull(); // climbers never see it
        (await w.Staff.Client.GetAsync($"/api/boulders/{created.Id}/highlight/source")).StatusCode.Should().Be(HttpStatusCode.OK);
        var maskRes = await w.Staff.Client.GetAsync($"/api/boulders/{created.Id}/highlight/mask");
        maskRes.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        var climber = await _f.UserAsync();
        (await climber.Client.GetAsync($"/api/boulders/{created.Id}/highlight/mask")).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        // Saved again without touching the photo (a grade changed): the highlight stays.
        (await w.Staff.Client.PutAsJsonAsync($"/api/boulders/{created.Id}", Body(w, result, null))).EnsureSuccessStatusCode();
        (await DetailAsync(w.Staff.Client, created.Id)).Highlight!.MaskPath.Should().Be(mask);

        // Edited again from the same original: a new photo and mask; the old mask goes, the original stays.
        var result2 = await w.Staff.UploadPhotoAsync(_f, w.Gym);
        var mask2 = await MaskAsync(w);
        (await w.Staff.Client.PutAsJsonAsync($"/api/boulders/{created.Id}", Body(w, result2, new { sourcePath = original, maskPath = mask2, settings = Settings }))).EnsureSuccessStatusCode();
        Stored(mask).Should().BeFalse();
        Stored(result).Should().BeFalse();
        Stored(original).Should().BeTrue();

        // A plain photo instead: nothing to go back to, and the boulder's own files go.
        var plain = await w.Staff.UploadPhotoAsync(_f, w.Gym);
        (await w.Staff.Client.PutAsJsonAsync($"/api/boulders/{created.Id}", Body(w, plain, null))).EnsureSuccessStatusCode();
        (await DetailAsync(w.Staff.Client, created.Id)).Highlight.Should().BeNull();
        Stored(mask2).Should().BeFalse();
        Stored(original).Should().BeFalse();
    }

    [Fact]
    public async Task A_wall_photo_a_boulder_was_highlighted_from_is_kept_when_the_sector_gets_a_new_one_and_never_deleted_with_the_boulder()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var wall = await WallAsync(w);
        (await w.Staff.Client.PutAsJsonAsync($"/api/sectors/{w.Sector.Id}/photo", new { path = wall })).EnsureSuccessStatusCode();
        var result = await w.Staff.UploadPhotoAsync(_f, w.Gym);
        var mask = await MaskAsync(w);
        var created = (await (await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders",
            Body(w, result, new { sourcePath = wall, maskPath = mask, settings = Settings }))).ReadAsync<BoulderDetailDto>())!;

        (await w.Staff.Client.PutAsJsonAsync($"/api/sectors/{w.Sector.Id}/photo", new { path = await WallAsync(w) })).EnsureSuccessStatusCode();
        Stored(wall).Should().BeTrue();

        (await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders/remove", new { boulderIds = new[] { created.Id }, notifyFollowers = false })).EnsureSuccessStatusCode();
        (await w.Staff.Client.DeleteAsync($"/api/boulders/{created.Id}")).EnsureSuccessStatusCode();
        Stored(mask).Should().BeFalse();
        Stored(wall).Should().BeTrue();
    }

    private async Task<BoulderSummaryDto> InListAsync(ClimbingWorld w, Guid id) =>
        (await (await _f.CreateClient().GetAsync($"/api/gyms/{w.Gym.Id}/boulders?sectorId={w.Sector.Id}")).ReadAsync<Application.Common.PagedResult<BoulderSummaryDto>>())!
            .Items.Single(b => b.Id == id);

    [Fact]
    public async Task Everyone_can_light_up_on_the_sector_photo_the_holds_of_a_boulder_highlighted_on_it_and_only_those()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var wall = await WallAsync(w);
        (await w.Staff.Client.PutAsJsonAsync($"/api/sectors/{w.Sector.Id}/photo", new { path = wall })).EnsureSuccessStatusCode();
        var mask = await MaskAsync(w);
        var onWall = (await (await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders",
            Body(w, await w.Staff.UploadPhotoAsync(_f, w.Gym), new { sourcePath = wall, maskPath = mask, settings = Settings }))).ReadAsync<BoulderDetailDto>())!;
        var ownPhoto = (await (await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders",
            Body(w, await w.Staff.UploadPhotoAsync(_f, w.Gym), new { sourcePath = await w.Staff.UploadPhotoAsync(_f, w.Gym), maskPath = await MaskAsync(w), settings = Settings }))).ReadAsync<BoulderDetailDto>())!;
        var plain = (await (await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders",
            Body(w, await w.Staff.UploadPhotoAsync(_f, w.Gym), null))).ReadAsync<BoulderDetailDto>())!;

        // Highlighted on the sector's photo: said so in the list, with the choices staff made, and its mask for anyone.
        var listed = await InListAsync(w, onWall.Id);
        listed.HoldsOnSectorPhoto.Should().BeTrue();
        listed.HoldsOnSectorPhotoLook.Should().Be(Settings);
        var res = await _f.CreateClient().GetAsync($"/api/boulders/{onWall.Id}/holds-on-sector-photo");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Content.Headers.ContentType!.MediaType.Should().Be("image/png");
        (await res.Content.ReadAsByteArrayAsync()).Should().Equal(Png);

        // On its own photo, or not highlighted at all: nothing to light up on the sector's.
        foreach (var other in new[] { ownPhoto.Id, plain.Id })
        {
            var item = await InListAsync(w, other);
            item.HoldsOnSectorPhoto.Should().BeFalse();
            item.HoldsOnSectorPhotoLook.Should().BeNull();
            (await _f.CreateClient().GetAsync($"/api/boulders/{other}/holds-on-sector-photo")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        // A new photo of the sector: the holds were on the old one.
        (await w.Staff.Client.PutAsJsonAsync($"/api/sectors/{w.Sector.Id}/photo", new { path = await WallAsync(w) })).EnsureSuccessStatusCode();
        (await InListAsync(w, onWall.Id)).HoldsOnSectorPhoto.Should().BeFalse();
        (await _f.CreateClient().GetAsync($"/api/boulders/{onWall.Id}/holds-on-sector-photo")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _f.CreateClient().GetAsync($"/api/boulders/{Guid.NewGuid()}/holds-on-sector-photo")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task A_boulder_taken_down_lights_nothing_up()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var wall = await WallAsync(w);
        (await w.Staff.Client.PutAsJsonAsync($"/api/sectors/{w.Sector.Id}/photo", new { path = wall })).EnsureSuccessStatusCode();
        var created = (await (await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders",
            Body(w, await w.Staff.UploadPhotoAsync(_f, w.Gym), new { sourcePath = wall, maskPath = await MaskAsync(w), settings = Settings }))).ReadAsync<BoulderDetailDto>())!;
        (await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders/remove", new { boulderIds = new[] { created.Id }, notifyFollowers = false })).EnsureSuccessStatusCode();
        (await _f.CreateClient().GetAsync($"/api/boulders/{created.Id}/holds-on-sector-photo")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Only_this_gyms_files_and_a_json_object_of_choices_are_accepted()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var other = await ClimbingWorld.CreateAsync(_f, "Other Gym");
        var original = await w.Staff.UploadPhotoAsync(_f, w.Gym);
        var result = await w.Staff.UploadPhotoAsync(_f, w.Gym);
        var mask = await MaskAsync(w);
        var otherMask = await MaskAsync(other);

        foreach (var highlight in new object[]
        {
            new { sourcePath = original, maskPath = otherMask, settings = Settings },                       // another gym's mask
            new { sourcePath = original, maskPath = original, settings = Settings },                        // a photo is not a mask
            new { sourcePath = mask, maskPath = mask, settings = Settings },                                // a mask is not a photo
            new { sourcePath = original, maskPath = $"gyms/{w.Gym.Id}/boulders/{Guid.NewGuid():N}.mask.png", settings = Settings }, // never uploaded
            new { sourcePath = original, maskPath = mask, settings = "not json" },
        })
            (await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders", Body(w, result, highlight))).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // A mask is a small PNG, and a mask is never a boulder's photo.
        (await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulder-photos", new { contentType = "image/jpeg", sizeBytes = 10, mask = true })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders", Body(w, mask, null))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
