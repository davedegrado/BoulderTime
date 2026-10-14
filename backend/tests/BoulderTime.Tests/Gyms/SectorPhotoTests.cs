using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Boulders;
using BoulderTime.Application.Common;
using BoulderTime.Application.Gyms;
using BoulderTime.Domain.Staff;
using BoulderTime.Tests.Boulders;
using BoulderTime.Tests.Climbing;
using BoulderTime.Tests.Infrastructure;

namespace BoulderTime.Tests.Gyms;

/// <summary>
/// A sector's wall photo, uploaded once and borrowed by the boulders set on it, each with its own card picture. The
/// file belongs to the sector: no boulder deletes it, and replacing it leaves those boulders without a photo, never
/// pointing at a file that is gone.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class SectorPhotoTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private async Task<string> UploadAsync(HttpClient client, string ticketUrl, object body)
    {
        var res = await client.PostAsJsonAsync(ticketUrl, body);
        res.EnsureSuccessStatusCode();
        var ticket = (await res.ReadAsync<UploadTicket>())!;
        var content = new ByteArrayContent(BoulderTestData.FakeJpeg);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        (await _f.CreateClient().PutAsync(ticket.UploadUrl, content)).EnsureSuccessStatusCode();
        return ticket.Path;
    }

    private Task<string> UploadWallAsync(ClimbingWorld w) =>
        UploadAsync(w.Staff.Client, $"/api/sectors/{w.Sector.Id}/photo-uploads", new { contentType = "image/jpeg", sizeBytes = BoulderTestData.FakeJpeg.Length });

    private Task<string> UploadThumbAsync(ClimbingWorld w) =>
        UploadAsync(w.Staff.Client, $"/api/gyms/{w.Gym.Id}/boulder-photos", new { contentType = "image/jpeg", sizeBytes = BoulderTestData.FakeJpeg.Length, thumbnail = true });

    private async Task<SectorDto> SetWallAsync(ClimbingWorld w, string? path)
    {
        var res = await w.Staff.Client.PutAsJsonAsync($"/api/sectors/{w.Sector.Id}/photo", new { path });
        res.EnsureSuccessStatusCode();
        return (await res.ReadAsync<SectorDto>())!;
    }

    private async Task<BoulderDetailDto> BoulderOnAsync(ClimbingWorld w, string photoPath, string? thumbnailPath)
    {
        var res = await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders", new
        {
            sectorId = w.Sector.Id, photoPath, thumbnailPath, holdColor = "BLUE",
            grades = new[] { new { gradeSystemId = w.Font.Id, gradeValueId = w.Font.Values.Single(v => v.Label == "6A").Id } },
        });
        res.StatusCode.Should().Be(HttpStatusCode.Created);
        return (await res.ReadAsync<BoulderDetailDto>())!;
    }

    private bool Stored(string path) => File.Exists(Path.Combine(_f.StorageRoot, StorageBuckets.BoulderImages, path));

    [Fact]
    public async Task Boulders_are_set_on_the_sectors_wall_photo_each_with_its_own_card_picture()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var wall = await UploadWallAsync(w);
        var sector = await SetWallAsync(w, wall);
        sector.PhotoPath.Should().Be(wall);
        sector.ImageUrl.Should().Contain(Path.GetFileName(wall));

        var thumbA = await UploadThumbAsync(w);
        var a = await BoulderOnAsync(w, wall, thumbA);
        var b = await BoulderOnAsync(w, wall, await UploadThumbAsync(w));
        a.PhotoUrl.Should().Contain(Path.GetFileName(wall));
        b.PhotoPath.Should().Be(wall);

        // Each card shows the boulder's own part of the wall.
        var list = (await (await _f.CreateClient().GetAsync($"/api/gyms/{w.Gym.Id}/boulders")).ReadAsync<PagedResult<BoulderSummaryDto>>())!;
        list.Items.Single(x => x.Id == a.Id).PhotoUrl.Should().Contain(Path.GetFileName(thumbA));

        // Staff read the wall's pixels through the API to cut the card picture out of it.
        (await w.Staff.Client.GetAsync($"/api/sectors/{w.Sector.Id}/photo")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await w.Staff.Client.GetAsync($"/api/boulders/{a.Id}/photo")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task No_boulder_deletes_the_wall_photo_it_borrows()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var wall = await UploadWallAsync(w);
        await SetWallAsync(w, wall);
        var moved = await BoulderOnAsync(w, wall, await UploadThumbAsync(w));
        var deleted = await BoulderOnAsync(w, wall, await UploadThumbAsync(w));

        // One boulder gets a photo of its own: the wall stays for the others.
        var own = await w.Staff.UploadPhotoAsync(_f, w.Gym);
        (await w.Staff.Client.PutAsJsonAsync($"/api/boulders/{moved.Id}", new
        {
            sectorId = w.Sector.Id, photoPath = own, holdColor = "BLUE", grades = Array.Empty<object>(),
        })).EnsureSuccessStatusCode();
        Stored(wall).Should().BeTrue();

        // Another is erased: its own card picture goes, the wall stays.
        (await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders/remove", new { boulderIds = new[] { deleted.Id }, notifyFollowers = false })).EnsureSuccessStatusCode();
        (await w.Staff.Client.DeleteAsync($"/api/boulders/{deleted.Id}")).EnsureSuccessStatusCode();
        Stored(wall).Should().BeTrue();
    }

    [Fact]
    public async Task Replacing_the_wall_photo_deletes_the_old_one_and_its_boulders_keep_only_their_card_picture()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var oldWall = await UploadWallAsync(w);
        await SetWallAsync(w, oldWall);
        var thumb = await UploadThumbAsync(w);
        var withThumb = await BoulderOnAsync(w, oldWall, thumb);
        var withoutThumb = await BoulderOnAsync(w, oldWall, null);
        var taken = await BoulderOnAsync(w, oldWall, null);
        (await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders/remove", new { boulderIds = new[] { taken.Id }, notifyFollowers = false })).EnsureSuccessStatusCode();

        // Asked first, so staff can be warned before the boulders lose it.
        var usage = (await (await w.Staff.Client.GetAsync($"/api/sectors/{w.Sector.Id}/photo/usage")).ReadAsync<SectorPhotoUsageDto>())!;
        usage.Should().Be(new SectorPhotoUsageDto(2, 1));

        var newWall = await UploadWallAsync(w);
        var sector = await SetWallAsync(w, newWall);
        sector.PhotoPath.Should().Be(newWall);
        Stored(oldWall).Should().BeFalse();

        // No boulder points at the deleted file: the card picture stays where there is one, otherwise a placeholder.
        var a = (await (await _f.CreateClient().GetAsync($"/api/boulders/{withThumb.Id}")).ReadAsync<BoulderDetailDto>())!;
        a.PhotoUrl.Should().BeNull();
        a.PhotoPath.Should().BeEmpty();
        a.ThumbnailUrl.Should().Contain(Path.GetFileName(thumb));
        Stored(thumb).Should().BeTrue();
        var list = (await (await _f.CreateClient().GetAsync($"/api/gyms/{w.Gym.Id}/boulders")).ReadAsync<PagedResult<BoulderSummaryDto>>())!;
        list.Items.Single(x => x.Id == withThumb.Id).PhotoUrl.Should().Contain(Path.GetFileName(thumb));
        list.Items.Single(x => x.Id == withoutThumb.Id).PhotoUrl.Should().BeNull();
        (await w.Staff.Client.GetAsync($"/api/boulders/{withoutThumb.Id}/photo")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // Such a boulder can still be corrected without a photo, but not set on the old wall again.
        var fix = new { sectorId = w.Sector.Id, photoPath = "", holdColor = "RED", grades = Array.Empty<object>() };
        (await w.Staff.Client.PutAsJsonAsync($"/api/boulders/{withoutThumb.Id}", fix)).EnsureSuccessStatusCode();
        (await w.Staff.Client.PutAsJsonAsync($"/api/boulders/{withoutThumb.Id}", fix with { photoPath = oldWall })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await w.Staff.Client.PutAsJsonAsync($"/api/boulders/{withoutThumb.Id}", fix with { photoPath = newWall })).EnsureSuccessStatusCode();

        // A new boulder still needs a photo.
        (await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders", fix)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Removing_the_wall_photo_works_like_replacing_it()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var wall = await UploadWallAsync(w);
        await SetWallAsync(w, wall);
        var boulder = await BoulderOnAsync(w, wall, null);

        var sector = await SetWallAsync(w, null);
        sector.PhotoPath.Should().BeNull();
        sector.ImageUrl.Should().BeNull();
        Stored(wall).Should().BeFalse();
        (await (await _f.CreateClient().GetAsync($"/api/boulders/{boulder.Id}")).ReadAsync<BoulderDetailDto>())!.PhotoUrl.Should().BeNull();
        (await w.Staff.Client.GetAsync($"/api/sectors/{w.Sector.Id}/photo")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Only_this_gyms_staff_manage_the_wall_photo_and_only_this_gyms_walls_can_be_used()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var climber = await _f.UserAsync();
        (await climber.Client.PostAsJsonAsync($"/api/sectors/{w.Sector.Id}/photo-uploads", new { contentType = "image/jpeg", sizeBytes = 12 })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await w.Staff.Client.PostAsJsonAsync($"/api/sectors/{w.Sector.Id}/photo-uploads", new { contentType = "image/gif", sizeBytes = 12 })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // A boulder photo is not a wall photo, and a wall photo never uploaded is refused.
        var boulderPhoto = await w.Staff.UploadPhotoAsync(_f, w.Gym);
        (await w.Staff.Client.PutAsJsonAsync($"/api/sectors/{w.Sector.Id}/photo", new { path = boulderPhoto })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await w.Staff.Client.PutAsJsonAsync($"/api/sectors/{w.Sector.Id}/photo", new { path = $"gyms/{w.Gym.Id}/sectors/{Guid.NewGuid():N}.jpg" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Another gym's wall can't hold this gym's boulders.
        var other = await ClimbingWorld.CreateAsync(_f, "Other Gym");
        var otherWall = await UploadWallAsync(other);
        await SetWallAsync(other, otherWall);
        await _f.StaffAsync(other.Gym, w.Staff, GymRole.Staff);
        (await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders", new
        {
            sectorId = w.Sector.Id, photoPath = otherWall, holdColor = "BLUE", grades = Array.Empty<object>(),
        })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
