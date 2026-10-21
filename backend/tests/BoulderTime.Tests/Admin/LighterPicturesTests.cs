using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Admin;
using BoulderTime.Application.Boulders;
using BoulderTime.Domain.Boulders;
using BoulderTime.Tests.Boulders;
using BoulderTime.Tests.Climbing;
using BoulderTime.Tests.Infrastructure;

namespace BoulderTime.Tests.Admin;

/// <summary>
/// A gym's existing boulder pictures made lighter: each one swapped for a copy encoded again on the administrator's
/// device. Until confirmed, the old files stay and the whole gym can be put back as it was. Platform admins only.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class LighterPicturesTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private static byte[] Jpeg(int size)
    {
        var bytes = new byte[size];
        BoulderTestData.FakeJpeg.CopyTo(bytes, 0);
        return bytes;
    }

    private bool Stored(string path) => File.Exists(Path.Combine(_f.StorageRoot, StorageBuckets.BoulderImages, path));
    private string Url(ClimbingWorld w, string rest = "") => $"/api/admin/gyms/{w.Gym.Id}/lighter-pictures{rest}";

    private async Task<string> UploadAsync(HttpClient client, string url, object body, byte[] bytes)
    {
        var res = await client.PostAsJsonAsync(url, body);
        res.EnsureSuccessStatusCode();
        var ticket = (await res.ReadAsync<UploadTicket>())!;
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        (await _f.CreateClient().PutAsync(ticket.UploadUrl, content)).EnsureSuccessStatusCode();
        return ticket.Path;
    }

    /// <summary>A boulder with a photo and a card picture of its own, as the app sets them.</summary>
    private async Task<(Guid Id, string Photo, string Thumb)> BoulderAsync(ClimbingWorld w)
    {
        var photo = await UploadAsync(w.Staff.Client, $"/api/gyms/{w.Gym.Id}/boulder-photos", new { contentType = "image/jpeg", sizeBytes = 4000 }, Jpeg(4000));
        var thumb = await UploadAsync(w.Staff.Client, $"/api/gyms/{w.Gym.Id}/boulder-photos", new { contentType = "image/jpeg", sizeBytes = 900, thumbnail = true }, Jpeg(900));
        var res = await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders",
            new { sectorId = w.Sector.Id, photoPath = photo, thumbnailPath = thumb, holdColor = "BLUE", grades = Array.Empty<object>() });
        res.EnsureSuccessStatusCode();
        return ((await res.ReadAsync<BoulderDetailDto>())!.Id, photo, thumb);
    }

    private async Task<RecompressionOverviewDto> OverviewAsync(TestUser admin, ClimbingWorld w) =>
        (await (await admin.Client.GetAsync(Url(w))).ReadAsync<RecompressionOverviewDto>())!;

    /// <summary>What the admin page does for one picture: read it, upload the lighter copy, swap it in.</summary>
    private async Task<HttpResponseMessage> MakeLighterAsync(TestUser admin, ClimbingWorld w, RecompressionCandidateDto c, int size)
    {
        (await admin.Client.GetAsync(Url(w, $"/{c.BoulderId}/{c.File}"))).StatusCode.Should().Be(HttpStatusCode.OK);
        var copy = await UploadAsync(admin.Client, Url(w, "/uploads"), new { file = c.File.ToString().ToUpperInvariant(), sizeBytes = size }, Jpeg(size));
        return await admin.Client.PostAsJsonAsync(Url(w), new { boulderId = c.BoulderId, file = c.File.ToString().ToUpperInvariant(), from = c.Path, to = copy });
    }

    private async Task<BoulderDetailDto> DetailAsync(Guid id) =>
        (await (await _f.CreateClient().GetAsync($"/api/boulders/{id}")).ReadAsync<BoulderDetailDto>())!;

    [Fact]
    public async Task Pictures_are_swapped_for_lighter_copies_and_can_all_be_put_back_until_confirmed()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var (id, photo, thumb) = await BoulderAsync(w);
        var admin = await _f.UserAsync(platformAdmin: true);

        var overview = await OverviewAsync(admin, w);
        overview.Pending.Should().BeEquivalentTo(new[]
        {
            new RecompressionCandidateDto(id, PhotoFile.Photo, photo),
            new RecompressionCandidateDto(id, PhotoFile.Thumbnail, thumb),
        });
        foreach (var c in overview.Pending)
        {
            var res = await MakeLighterAsync(admin, w, c, c.File == PhotoFile.Photo ? 1500 : 400);
            res.EnsureSuccessStatusCode();
        }

        // Everyone, old apps included, now gets the lighter files; the old ones are still there.
        var detail = await DetailAsync(id);
        detail.PhotoPath.Should().NotBe(photo).And.EndWith(".jpg");
        detail.PhotoUrl.Should().EndWith(detail.PhotoPath);
        Stored(photo).Should().BeTrue();
        Stored(thumb).Should().BeTrue();
        overview = await OverviewAsync(admin, w);
        overview.Pending.Should().BeEmpty(); // a copy is never made lighter again
        overview.Should().BeEquivalentTo(new { Replaced = 2, BytesBefore = 4900L, BytesAfter = 1900L, Confirmed = 0 });

        // Put back: the boulder has its old files again and the copies are gone.
        (await admin.Client.PostAsJsonAsync(Url(w, "/undo"), new { gymSlug = "wrong" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var undo = await admin.Client.PostAsJsonAsync(Url(w, "/undo"), new { gymSlug = w.Gym.Slug });
        (await undo.ReadAsync<RecompressionResultDto>()).Should().Be(new RecompressionResultDto(2, 0, 1900));
        var restored = await DetailAsync(id);
        restored.PhotoPath.Should().Be(photo);
        Stored(detail.PhotoPath).Should().BeFalse();
        (await OverviewAsync(admin, w)).Pending.Should().HaveCount(2);

        // Done again and confirmed: the old files go, and there is nothing left to put back.
        foreach (var c in (await OverviewAsync(admin, w)).Pending)
            (await MakeLighterAsync(admin, w, c, c.File == PhotoFile.Photo ? 1500 : 400)).EnsureSuccessStatusCode();
        var confirm = await admin.Client.PostAsJsonAsync(Url(w, "/confirm"), new { gymSlug = w.Gym.Slug });
        (await confirm.ReadAsync<RecompressionResultDto>()).Should().Be(new RecompressionResultDto(2, 0, 4900));
        Stored(photo).Should().BeFalse();
        Stored(thumb).Should().BeFalse();
        Stored((await DetailAsync(id)).PhotoPath).Should().BeTrue();
        var again = await admin.Client.PostAsJsonAsync(Url(w, "/undo"), new { gymSlug = w.Gym.Slug });
        (await again.ReadAsync<RecompressionResultDto>())!.Changed.Should().Be(0);
        (await OverviewAsync(admin, w)).Should().BeEquivalentTo(new { Pending = Array.Empty<RecompressionCandidateDto>(), Replaced = 0, Confirmed = 2 });
    }

    [Fact]
    public async Task Only_platform_admins_can_do_it()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var (id, _, _) = await BoulderAsync(w);
        foreach (var someone in new[] { w.Staff, await _f.UserAsync() })
        {
            (await someone.Client.GetAsync(Url(w))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await someone.Client.GetAsync(Url(w, $"/{id}/photo"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await someone.Client.PostAsJsonAsync(Url(w, "/uploads"), new { file = "PHOTO", sizeBytes = 100 })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await someone.Client.PostAsJsonAsync(Url(w, "/undo"), new { gymSlug = w.Gym.Slug })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await someone.Client.PostAsJsonAsync(Url(w, "/confirm"), new { gymSlug = w.Gym.Slug })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        (await _f.CreateClient().GetAsync(Url(w))).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Wall_photos_are_left_alone_and_a_copy_that_saves_nothing_or_comes_too_late_is_refused()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var admin = await _f.UserAsync(platformAdmin: true);
        var wall = await UploadAsync(w.Staff.Client, $"/api/sectors/{w.Sector.Id}/photo-uploads", new { contentType = "image/jpeg", sizeBytes = 3000 }, Jpeg(3000));
        (await w.Staff.Client.PutAsJsonAsync($"/api/sectors/{w.Sector.Id}/photo", new { path = wall })).EnsureSuccessStatusCode();
        (await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders",
            new { sectorId = w.Sector.Id, photoPath = wall, holdColor = "BLUE", grades = Array.Empty<object>() })).EnsureSuccessStatusCode();
        (await OverviewAsync(admin, w)).Pending.Should().BeEmpty();

        var (id, photo, _) = await BoulderAsync(w);
        var candidate = new RecompressionCandidateDto(id, PhotoFile.Photo, photo);

        // Not lighter: refused, and the copy deleted.
        var heavier = await MakeLighterAsync(admin, w, candidate, 5000);
        heavier.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await DetailAsync(id)).PhotoPath.Should().Be(photo);

        // Staff changed the photo meanwhile: their photo stays.
        var theirs = await w.Staff.UploadPhotoAsync(_f, w.Gym);
        (await w.Staff.Client.PutAsJsonAsync($"/api/boulders/{id}",
            new { sectorId = w.Sector.Id, photoPath = theirs, holdColor = "BLUE", grades = Array.Empty<object>() })).EnsureSuccessStatusCode();
        (await MakeLighterAsync(admin, w, candidate, 1000)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await DetailAsync(id)).PhotoPath.Should().Be(theirs);
    }

    [Fact]
    public async Task Putting_back_leaves_a_picture_staff_changed_since_and_confirming_deletes_its_old_file()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var admin = await _f.UserAsync(platformAdmin: true);
        var (id, photo, _) = await BoulderAsync(w);
        (await MakeLighterAsync(admin, w, new RecompressionCandidateDto(id, PhotoFile.Photo, photo), 1000)).EnsureSuccessStatusCode();

        var theirs = await w.Staff.UploadPhotoAsync(_f, w.Gym);
        (await w.Staff.Client.PutAsJsonAsync($"/api/boulders/{id}",
            new { sectorId = w.Sector.Id, photoPath = theirs, holdColor = "BLUE", grades = Array.Empty<object>() })).EnsureSuccessStatusCode();

        var undo = await admin.Client.PostAsJsonAsync(Url(w, "/undo"), new { gymSlug = w.Gym.Slug });
        (await undo.ReadAsync<RecompressionResultDto>())!.Should().BeEquivalentTo(new { Changed = 0, Skipped = 1 });
        (await DetailAsync(id)).PhotoPath.Should().Be(theirs);
        Stored(photo).Should().BeTrue();

        (await admin.Client.PostAsJsonAsync(Url(w, "/confirm"), new { gymSlug = w.Gym.Slug })).EnsureSuccessStatusCode();
        Stored(photo).Should().BeFalse();
        Stored(theirs).Should().BeTrue();
    }
}
