using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Boulders;
using BoulderTime.Domain.Boulders;
using BoulderTime.Tests.Climbing;
using BoulderTime.Tests.Infrastructure;

namespace BoulderTime.Tests.Boulders;

/// <summary>
/// How a card picture was made (ADR-068): kept with the picture for the editor, shown to staff only, and never set or
/// kept wrongly by an app that doesn't send it (the 1.0 editor).
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class ThumbnailModeTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private async Task<string> UploadAsync(ClimbingWorld w, bool thumbnail)
    {
        var bytes = new byte[thumbnail ? 900 : 4000];
        BoulderTestData.FakeJpeg.CopyTo(bytes, 0);
        var res = await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulder-photos", new { contentType = "image/jpeg", sizeBytes = bytes.Length, thumbnail });
        res.EnsureSuccessStatusCode();
        var ticket = (await res.ReadAsync<UploadTicket>())!;
        var content = new ByteArrayContent(bytes);
        content.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        (await _f.CreateClient().PutAsync(ticket.UploadUrl, content)).EnsureSuccessStatusCode();
        return ticket.Path;
    }

    private static object Body(ClimbingWorld w, string photo, string? thumb, string? mode) => mode is null
        ? new { sectorId = w.Sector.Id, photoPath = photo, thumbnailPath = thumb, holdColor = "BLUE", grades = Array.Empty<object>() }
        : new { sectorId = w.Sector.Id, photoPath = photo, thumbnailPath = thumb, holdColor = "BLUE", grades = Array.Empty<object>(), thumbnailMode = mode };

    private static async Task<BoulderDetailDto> Detail(HttpClient client, Guid id) =>
        (await (await client.GetAsync($"/api/boulders/{id}")).ReadAsync<BoulderDetailDto>())!;

    [Fact]
    public async Task The_choice_is_kept_with_the_picture_and_shown_to_staff_only()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var photo = await UploadAsync(w, false);
        var res = await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders", Body(w, photo, await UploadAsync(w, true), "FIT"));
        res.EnsureSuccessStatusCode();
        var id = (await res.ReadAsync<BoulderDetailDto>())!.Id;

        (await Detail(w.Staff.Client, id)).ThumbnailMode.Should().Be(ThumbnailMode.Fit);
        (await Detail((await _f.UserAsync()).Client, id)).ThumbnailMode.Should().BeNull("climbers don't need it");

        // A new picture with a new choice.
        (await w.Staff.Client.PutAsJsonAsync($"/api/boulders/{id}", Body(w, photo, await UploadAsync(w, true), "FILL_TOP"))).EnsureSuccessStatusCode();
        (await Detail(w.Staff.Client, id)).ThumbnailMode.Should().Be(ThumbnailMode.FillTop);
    }

    [Fact]
    public async Task An_app_that_doesnt_send_it_keeps_it_when_the_picture_stays_and_forgets_it_when_the_picture_changes()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var photo = await UploadAsync(w, false);
        var res = await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders", Body(w, photo, await UploadAsync(w, true), "FILL_BOTTOM"));
        var id = (await res.ReadAsync<BoulderDetailDto>())!.Id;

        // The 1.0 editor saves other changes: no picture, no choice sent — the picture and its choice stay.
        (await w.Staff.Client.PutAsJsonAsync($"/api/boulders/{id}", Body(w, photo, null, null))).EnsureSuccessStatusCode();
        (await Detail(w.Staff.Client, id)).ThumbnailMode.Should().Be(ThumbnailMode.FillBottom);

        // It makes a new picture: the choice no longer describes it.
        (await w.Staff.Client.PutAsJsonAsync($"/api/boulders/{id}", Body(w, photo, await UploadAsync(w, true), null))).EnsureSuccessStatusCode();
        (await Detail(w.Staff.Client, id)).ThumbnailMode.Should().BeNull();

        // A new photo without a picture: no picture, no choice.
        (await w.Staff.Client.PutAsJsonAsync($"/api/boulders/{id}", Body(w, photo, await UploadAsync(w, true), "CUSTOM"))).EnsureSuccessStatusCode();
        (await w.Staff.Client.PutAsJsonAsync($"/api/boulders/{id}", Body(w, await UploadAsync(w, false), null, null))).EnsureSuccessStatusCode();
        var after = await Detail(w.Staff.Client, id);
        after.ThumbnailUrl.Should().BeNull();
        after.ThumbnailMode.Should().BeNull();
    }

    [Fact]
    public async Task A_choice_it_doesnt_know_is_refused_and_one_without_a_picture_means_nothing()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var photo = await UploadAsync(w, false);
        (await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders", Body(w, photo, null, "STRETCH"))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var res = await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders", Body(w, photo, null, "FIT"));
        res.EnsureSuccessStatusCode();
        (await Detail(w.Staff.Client, (await res.ReadAsync<BoulderDetailDto>())!.Id)).ThumbnailMode.Should().BeNull();
    }
}
