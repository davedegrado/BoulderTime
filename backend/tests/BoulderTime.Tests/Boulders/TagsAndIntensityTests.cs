using System.Net;
using System.Net.Http.Json;
using BoulderTime.Application.Boulders;
using BoulderTime.Application.Common;
using BoulderTime.Domain.Boulders;
using BoulderTime.Tests.Climbing;
using BoulderTime.Tests.Infrastructure;

namespace BoulderTime.Tests.Boulders;

/// <summary>Each gym's own tags, and the intensity staff give a boulder apart from its grade (ADR-046).</summary>
[Collection(DatabaseCollection.Name)]
public sealed class TagsAndIntensityTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private static async Task<GymTagDto> TagAsync(TestUser staff, Guid gymId, string name)
    {
        var res = await staff.Client.PostAsJsonAsync($"/api/gyms/{gymId}/tags", new { name });
        res.EnsureSuccessStatusCode();
        return (await res.ReadAsync<GymTagDto>())!;
    }

    private static object Body(ClimbingWorld w, string photo, object? tagIds, string? intensity) => new
    {
        sectorId = w.Sector.Id, photoPath = photo, holdColor = "BLUE",
        grades = new[] { new { gradeSystemId = w.Font.Id, gradeValueId = w.Font.Values.Single(v => v.Label == "6A").Id } },
        tagIds, intensity,
    };

    [Fact]
    public async Task Staff_keep_their_own_tags_and_a_name_differing_only_in_case_accents_or_spaces_is_the_same_tag()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var slab = await TagAsync(w.Staff, w.Gym.Id, "  Placca ");
        slab.Name.Should().Be("Placca");

        var dup = await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/tags", new { name = "placcà" });
        dup.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await dup.Content.ReadAsStringAsync()).Should().Contain("Placca");

        // Another gym may have its own "Placca"; its staff can't touch ours, nor can a climber.
        var other = await ClimbingWorld.CreateAsync(_f, "Other gym");
        await TagAsync(other.Staff, other.Gym.Id, "Placca");
        (await other.Staff.Client.PatchAsJsonAsync($"/api/tags/{slab.Id}", new { name = "Mine" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await other.Staff.Client.DeleteAsync($"/api/tags/{slab.Id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var climber = await _f.UserAsync();
        (await climber.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/tags", new { name = "Tecnico" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var listed = (await (await _f.CreateClient().GetAsync($"/api/gyms/{w.Gym.Id}/tags")).ReadAsync<List<GymTagDto>>())!;
        listed.Select(t => t.Name).Should().Equal("Placca");
    }

    [Fact]
    public async Task A_boulder_carries_tags_and_an_intensity_and_both_filter_the_list()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var slab = await TagAsync(w.Staff, w.Gym.Id, "Placca");
        var technical = await TagAsync(w.Staff, w.Gym.Id, "Tecnico");
        var both = await (await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders",
            Body(w, await w.Staff.UploadPhotoAsync(_f, w.Gym), new[] { slab.Id, technical.Id }, "HIGH"))).ReadAsync<BoulderDetailDto>();
        both!.Intensity.Should().Be(BoulderIntensity.High);
        both.Tags!.Select(t => t.Name).Should().Equal("Placca", "Tecnico");
        var oneTag = await (await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders",
            Body(w, await w.Staff.UploadPhotoAsync(_f, w.Gym), new[] { slab.Id }, "LOW"))).ReadAsync<BoulderDetailDto>();
        var plain = await w.BoulderAsync();
        plain.Intensity.Should().BeNull();
        plain.Tags!.Should().BeEmpty();

        async Task<List<Guid>> Ids(string query) =>
            (await (await _f.CreateClient().GetAsync($"/api/gyms/{w.Gym.Id}/boulders?{query}")).ReadAsync<PagedResult<BoulderSummaryDto>>())!
                .Items.Select(b => b.Id).ToList();

        (await Ids($"tagId={slab.Id}")).Should().BeEquivalentTo(new[] { both.Id, oneTag!.Id });
        (await Ids($"tagId={slab.Id}&tagId={technical.Id}")).Should().Equal(both.Id); // every tag chosen narrows
        (await Ids("intensity=LOW")).Should().Equal(oneTag.Id);
        (await Ids("intensity=LOW&intensity=HIGH")).Should().BeEquivalentTo(new[] { both.Id, oneTag.Id });
        (await Ids("")).Should().HaveCount(3);
    }

    [Fact]
    public async Task Tags_must_be_the_gyms_own_at_most_six_and_the_intensity_one_of_the_three()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var other = await ClimbingWorld.CreateAsync(_f, "Other gym");
        var foreign = await TagAsync(other.Staff, other.Gym.Id, "Placca");
        var photo = await w.Staff.UploadPhotoAsync(_f, w.Gym);

        (await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders", Body(w, photo, new[] { foreign.Id }, null)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var seven = new List<Guid>();
        for (var i = 0; i < 7; i++) seven.Add((await TagAsync(w.Staff, w.Gym.Id, $"Tag {i}")).Id);
        (await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders", Body(w, photo, seven, null)))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders", Body(w, photo, null, "EXTREME")))
            .StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Renaming_a_tag_renames_it_on_every_boulder_and_deleting_it_takes_it_off()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var tag = await TagAsync(w.Staff, w.Gym.Id, "Dinamico");
        var b = await (await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders",
            Body(w, await w.Staff.UploadPhotoAsync(_f, w.Gym), new[] { tag.Id }, "MEDIUM"))).ReadAsync<BoulderDetailDto>();

        var renamed = (await (await w.Staff.Client.PatchAsJsonAsync($"/api/tags/{tag.Id}", new { name = "Dinamicissimo" })).ReadAsync<GymTagDto>())!;
        renamed.Boulders.Should().Be(1);
        (await (await _f.CreateClient().GetAsync($"/api/boulders/{b!.Id}")).ReadAsync<BoulderDetailDto>())!.Tags!.Single().Name.Should().Be("Dinamicissimo");

        // An edit from an app that doesn't know about tags leaves them alone.
        var edit = Body(w, b.PhotoPath, null, "MEDIUM");
        (await (await w.Staff.Client.PutAsJsonAsync($"/api/boulders/{b.Id}", edit)).ReadAsync<BoulderDetailDto>())!.Tags!.Should().HaveCount(1);

        (await w.Staff.Client.DeleteAsync($"/api/tags/{tag.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var after = (await (await _f.CreateClient().GetAsync($"/api/boulders/{b.Id}")).ReadAsync<BoulderDetailDto>())!;
        after.Tags!.Should().BeEmpty();
        after.Intensity.Should().Be(BoulderIntensity.Medium);
    }
}
