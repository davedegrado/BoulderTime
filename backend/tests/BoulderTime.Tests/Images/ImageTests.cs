using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Boulders;
using BoulderTime.Application.Common;
using BoulderTime.Application.Gyms;
using BoulderTime.Application.Users;
using BoulderTime.Domain.Staff;
using BoulderTime.Tests.Boulders;
using BoulderTime.Tests.Climbing;
using BoulderTime.Tests.Infrastructure;

namespace BoulderTime.Tests.Images;

[Collection(DatabaseCollection.Name)]
public sealed class ImageTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 1, 2, 3];

    private async Task<string> UploadAsync(HttpClient client, string ticketUrl, object body)
    {
        var res = await client.PostAsJsonAsync(ticketUrl, body);
        res.EnsureSuccessStatusCode();
        var ticket = (await res.ReadAsync<UploadTicket>())!;
        var content = new ByteArrayContent(Jpeg);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        (await _f.CreateClient().PutAsync(ticket.UploadUrl, content)).EnsureSuccessStatusCode();
        return ticket.Path;
    }

    [Fact]
    public async Task Users_set_replace_and_remove_their_avatar_and_old_files_are_deleted()
    {
        var me = await _f.UserAsync();
        var first = await UploadAsync(me.Client, "/api/users/me/avatar-uploads", new { contentType = "image/jpeg", sizeBytes = Jpeg.Length });

        var withAvatar = (await (await me.Client.PutAsJsonAsync("/api/users/me/avatar", new { path = first })).ReadAsync<CurrentUserDto>())!;
        withAvatar.AvatarUrl.Should().NotBeNull();
        (await _f.CreateClient().GetAsync(withAvatar.AvatarUrl)).StatusCode.Should().Be(HttpStatusCode.OK);

        var second = await UploadAsync(me.Client, "/api/users/me/avatar-uploads", new { contentType = "image/jpeg", sizeBytes = Jpeg.Length });
        (await me.Client.PutAsJsonAsync("/api/users/me/avatar", new { path = second })).EnsureSuccessStatusCode();
        File.Exists(Path.Combine(_f.StorageRoot, StorageBuckets.Avatars, first)).Should().BeFalse();

        var removed = (await (await me.Client.PutAsJsonAsync("/api/users/me/avatar", new { path = (string?)null })).ReadAsync<CurrentUserDto>())!;
        removed.AvatarUrl.Should().BeNull();
        File.Exists(Path.Combine(_f.StorageRoot, StorageBuckets.Avatars, second)).Should().BeFalse();
    }

    [Fact]
    public async Task Nobody_can_use_another_users_avatar_file()
    {
        var alice = await _f.UserAsync();
        var bob = await _f.UserAsync();
        var alicePath = await UploadAsync(alice.Client, "/api/users/me/avatar-uploads", new { contentType = "image/jpeg", sizeBytes = Jpeg.Length });

        (await bob.Client.PutAsJsonAsync("/api/users/me/avatar", new { path = alicePath })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await bob.Client.PostAsJsonAsync("/api/users/me/avatar-uploads", new { contentType = "image/gif", sizeBytes = 10 })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Gym_admins_manage_logo_and_cover_and_staff_cannot()
    {
        var w = await ClimbingWorld.CreateAsync(_f); // w.Staff is OWNER
        var staff = await _f.UserAsync();
        await _f.StaffAsync(w.Gym, staff, GymRole.Staff);

        (await staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/image-uploads", new { kind = "LOGO", contentType = "image/jpeg", sizeBytes = 7 })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var logo = await UploadAsync(w.Staff.Client, $"/api/gyms/{w.Gym.Id}/image-uploads", new { kind = "LOGO", contentType = "image/jpeg", sizeBytes = Jpeg.Length });
        var cover = await UploadAsync(w.Staff.Client, $"/api/gyms/{w.Gym.Id}/image-uploads", new { kind = "COVER", contentType = "image/jpeg", sizeBytes = Jpeg.Length });
        (await w.Staff.Client.PutAsJsonAsync($"/api/gyms/{w.Gym.Id}/logo", new { path = cover })).StatusCode.Should().Be(HttpStatusCode.BadRequest); // cover file isn't a logo
        (await w.Staff.Client.PutAsJsonAsync($"/api/gyms/{w.Gym.Id}/logo", new { path = logo })).EnsureSuccessStatusCode();
        (await w.Staff.Client.PutAsJsonAsync($"/api/gyms/{w.Gym.Id}/cover", new { path = cover })).EnsureSuccessStatusCode();

        var gym = (await (await _f.CreateClient().GetAsync($"/api/gyms/{w.Gym.Slug}")).ReadAsync<GymDetailDto>())!;
        gym.LogoUrl.Should().NotBeNull();
        gym.CoverImageUrl.Should().NotBeNull();
        var search = (await (await _f.CreateClient().GetAsync("/api/gyms?q=Crimp")).ReadAsync<PagedResult<GymSummaryDto>>())!;
        search.Items.Should().ContainSingle(g => g.LogoUrl == gym.LogoUrl);
    }

    [Fact]
    public async Task Boulder_lists_use_the_small_thumbnail_and_the_detail_page_uses_the_full_photo()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var photo = await w.Staff.UploadPhotoAsync(_f, w.Gym);
        var thumb = await UploadAsync(w.Staff.Client, $"/api/gyms/{w.Gym.Id}/boulder-photos", new { contentType = "image/jpeg", sizeBytes = Jpeg.Length, thumbnail = true });
        thumb.Should().Contain(".thumb.");
        (await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulder-photos", new { contentType = "image/jpeg", sizeBytes = 900_000, thumbnail = true })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var body = new
        {
            sectorId = w.Sector.Id, photoPath = photo, thumbnailPath = thumb, holdColor = "BLUE",
            grades = new[] { new { gradeSystemId = w.Font.Id, gradeValueId = w.Font.Values[5].Id } },
        };
        var created = (await (await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders", body)).ReadAsync<BoulderDetailDto>())!;

        created.PhotoUrl.Should().Contain(Path.GetFileName(photo));
        var list = (await (await _f.CreateClient().GetAsync($"/api/gyms/{w.Gym.Id}/boulders")).ReadAsync<PagedResult<BoulderSummaryDto>>())!;
        list.Items.Single().PhotoUrl.Should().Contain(Path.GetFileName(thumb));

        // A thumbnail can't be used as the main photo.
        var swapped = new { body.sectorId, photoPath = thumb, body.holdColor, body.grades };
        (await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders", swapped)).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        // Replacing the photo deletes the old photo and its thumbnail.
        var newPhoto = await w.Staff.UploadPhotoAsync(_f, w.Gym);
        var update = new { body.sectorId, photoPath = newPhoto, body.holdColor, body.grades };
        (await w.Staff.Client.PutAsJsonAsync($"/api/boulders/{created.Id}", update)).EnsureSuccessStatusCode();
        File.Exists(Path.Combine(_f.StorageRoot, StorageBuckets.BoulderImages, thumb)).Should().BeFalse();
    }

    [Fact]
    public async Task Staff_can_pick_another_part_of_the_same_photo_for_the_cards()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var photo = await w.Staff.UploadPhotoAsync(_f, w.Gym);
        var first = await UploadAsync(w.Staff.Client, $"/api/gyms/{w.Gym.Id}/boulder-photos", new { contentType = "image/jpeg", sizeBytes = Jpeg.Length, thumbnail = true });
        var body = new
        {
            sectorId = w.Sector.Id, photoPath = photo, thumbnailPath = first, holdColor = "BLUE",
            grades = new[] { new { gradeSystemId = w.Font.Id, gradeValueId = w.Font.Values[5].Id } },
        };
        var created = (await (await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders", body)).ReadAsync<BoulderDetailDto>())!;
        created.ThumbnailUrl.Should().Contain(Path.GetFileName(first));

        // The same photo with a new crop: the cards change, the photo stays, the old crop is deleted.
        var second = await UploadAsync(w.Staff.Client, $"/api/gyms/{w.Gym.Id}/boulder-photos", new { contentType = "image/jpeg", sizeBytes = Jpeg.Length, thumbnail = true });
        var updated = (await (await w.Staff.Client.PutAsJsonAsync($"/api/boulders/{created.Id}", body with { thumbnailPath = second })).ReadAsync<BoulderDetailDto>())!;
        updated.PhotoPath.Should().Be(photo);
        updated.ThumbnailUrl.Should().Contain(Path.GetFileName(second));
        File.Exists(Path.Combine(_f.StorageRoot, StorageBuckets.BoulderImages, first)).Should().BeFalse();
        File.Exists(Path.Combine(_f.StorageRoot, StorageBuckets.BoulderImages, photo)).Should().BeTrue();

        // Saving other changes without a thumbnail keeps the one there is.
        var kept = (await (await w.Staff.Client.PutAsJsonAsync($"/api/boulders/{created.Id}", new { body.sectorId, body.photoPath, holdColor = "RED", body.grades })).ReadAsync<BoulderDetailDto>())!;
        kept.ThumbnailUrl.Should().Contain(Path.GetFileName(second));
    }

    [Fact]
    public async Task Staff_can_read_the_full_photo_through_the_api_to_cut_the_card_picture()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var b = await w.BoulderAsync();

        var res = await w.Staff.Client.GetAsync($"/api/boulders/{b.Id}/photo");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
        res.Content.Headers.ContentType!.MediaType.Should().StartWith("image/");
        (await res.Content.ReadAsByteArrayAsync()).Should().NotBeEmpty();

        // Everyone else loads the photo from its public address; this way in is for the gym's staff.
        var climber = await _f.UserAsync();
        (await climber.Client.GetAsync($"/api/boulders/{b.Id}/photo")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _f.CreateClient().GetAsync($"/api/boulders/{b.Id}/photo")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Staff_upload_the_floor_plan_with_its_size_and_climbers_see_it()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var staff = await _f.UserAsync();
        await _f.StaffAsync(w.Gym, staff, GymRole.Staff);

        // Unlike the logo, the plan is the sectors' business, so plain staff may set it.
        var plan = await UploadAsync(staff.Client, $"/api/gyms/{w.Gym.Id}/image-uploads", new { kind = "FLOOR_PLAN", contentType = "image/jpeg", sizeBytes = Jpeg.Length });
        plan.Should().Contain("/floorplan/");
        (await staff.Client.PutAsJsonAsync($"/api/gyms/{w.Gym.Id}/floor-plan", new { path = plan })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var set = (await (await staff.Client.PutAsJsonAsync($"/api/gyms/{w.Gym.Id}/floor-plan", new { path = plan, width = 1200, height = 1600 })).ReadAsync<GymDetailDto>())!;
        set.FloorPlanUrl.Should().Contain(Path.GetFileName(plan));

        var seen = (await (await _f.CreateClient().GetAsync($"/api/gyms/{w.Gym.Slug}")).ReadAsync<GymDetailDto>())!;
        seen.FloorPlanUrl.Should().Be(set.FloorPlanUrl);
        seen.FloorPlanWidth.Should().Be(1200);
        seen.FloorPlanHeight.Should().Be(1600);

        var climber = await _f.UserAsync();
        (await climber.Client.PutAsJsonAsync($"/api/gyms/{w.Gym.Id}/floor-plan", new { path = (string?)null })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var removed = (await (await staff.Client.PutAsJsonAsync($"/api/gyms/{w.Gym.Id}/floor-plan", new { path = (string?)null })).ReadAsync<GymDetailDto>())!;
        removed.FloorPlanUrl.Should().BeNull();
        removed.FloorPlanWidth.Should().BeNull();
        File.Exists(Path.Combine(_f.StorageRoot, StorageBuckets.GymImages, plan)).Should().BeFalse();
    }
}
