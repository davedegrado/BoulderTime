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
/// A sector's wall photo, uploaded once and borrowed by the boulders set on it, each with its own card picture. No
/// boulder deletes it, and replacing it keeps the old file for the boulders already set on it.
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
    public async Task Replacing_the_wall_photo_keeps_the_old_one_for_the_boulders_already_set_on_it()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var oldWall = await UploadWallAsync(w);
        await SetWallAsync(w, oldWall);
        var onTheWall = await BoulderOnAsync(w, oldWall, await UploadThumbAsync(w));
        var taken = await BoulderOnAsync(w, oldWall, null);
        (await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders/remove", new { boulderIds = new[] { taken.Id }, notifyFollowers = false })).EnsureSuccessStatusCode();

        var newWall = await UploadWallAsync(w);
        (await SetWallAsync(w, newWall)).PhotoPath.Should().Be(newWall);

        // Climbers' history and the boulders taken down still show the wall they were set on.
        Stored(oldWall).Should().BeTrue();
        foreach (var id in new[] { onTheWall.Id, taken.Id })
        {
            var b = (await (await _f.CreateClient().GetAsync($"/api/boulders/{id}")).ReadAsync<BoulderDetailDto>())!;
            b.PhotoPath.Should().Be(oldWall);
            (await _f.CreateClient().GetAsync(b.PhotoUrl)).StatusCode.Should().Be(HttpStatusCode.OK);
        }

        // Such a boulder can still be corrected, keeping its wall.
        (await w.Staff.Client.PutAsJsonAsync($"/api/boulders/{onTheWall.Id}", new
        {
            sectorId = w.Sector.Id, photoPath = oldWall, holdColor = "RED", grades = Array.Empty<object>(),
        })).EnsureSuccessStatusCode();

        // New boulders go on the current wall, not the old one.
        (await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders", new
        {
            sectorId = w.Sector.Id, photoPath = oldWall, holdColor = "BLUE", grades = Array.Empty<object>(),
        })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await BoulderOnAsync(w, newWall, null);
    }

    [Fact]
    public async Task A_wall_photo_no_boulder_was_set_on_is_deleted_when_replaced_or_removed()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var wrong = await UploadWallAsync(w);
        await SetWallAsync(w, wrong);
        var right = await UploadWallAsync(w);
        await SetWallAsync(w, right);
        Stored(wrong).Should().BeFalse();

        var sector = await SetWallAsync(w, null);
        sector.PhotoPath.Should().BeNull();
        sector.ImageUrl.Should().BeNull();
        Stored(right).Should().BeFalse();
        (await w.Staff.Client.GetAsync($"/api/sectors/{w.Sector.Id}/photo")).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Removing_the_wall_photo_keeps_it_for_the_boulders_set_on_it()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var wall = await UploadWallAsync(w);
        await SetWallAsync(w, wall);
        var boulder = await BoulderOnAsync(w, wall, null);

        (await SetWallAsync(w, null)).PhotoPath.Should().BeNull();
        Stored(wall).Should().BeTrue();
        (await (await _f.CreateClient().GetAsync($"/api/boulders/{boulder.Id}")).ReadAsync<BoulderDetailDto>())!.PhotoPath.Should().Be(wall);
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
