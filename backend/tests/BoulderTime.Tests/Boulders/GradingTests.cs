using System.Net;
using System.Net.Http.Json;
using BoulderTime.Application.Grading;
using BoulderTime.Domain.Grading;
using BoulderTime.Domain.Staff;
using BoulderTime.Tests.Infrastructure;

namespace BoulderTime.Tests.Boulders;

[Collection(DatabaseCollection.Name)]
public sealed class GradingTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    [Fact]
    public async Task A_gym_can_run_several_grading_systems_created_from_presets_in_difficulty_order()
    {
        var gym = await _f.GymAsync();
        var admin = await _f.UserAsync();
        await _f.StaffAsync(gym, admin, GymRole.Admin);

        var font = await admin.GradeSystemAsync(gym, "FONTAINEBLEAU");
        var color = await admin.GradeSystemAsync(gym, "COLOR");

        font.Values.Select(v => v.Label).Take(6).Should().Equal("3", "4", "4+", "5", "5+", "6A");
        font.Values.Select(v => v.Rank).Should().BeInAscendingOrder();
        color.Type.Should().Be(GradeSystemType.Color);
        color.Values.Should().OnlyContain(v => v.ColorHex != null);

        var listed = await (await _f.CreateClient().GetAsync($"/api/gyms/{gym.Id}/grade-systems")).ReadAsync<List<GradeSystemDto>>();
        listed!.Select(s => s.Name).Should().Equal("Fontainebleau", "Colour");
    }

    [Fact]
    public async Task Staff_role_cannot_change_grading_systems()
    {
        var gym = await _f.GymAsync();
        var staff = await _f.UserAsync();
        await _f.StaffAsync(gym, staff, GymRole.Staff);

        (await staff.Client.PostAsJsonAsync($"/api/gyms/{gym.Id}/grade-systems", new { type = "V_SCALE" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Colour_grades_require_a_grade_colour_and_labels_must_be_unique()
    {
        var gym = await _f.GymAsync();
        var admin = await _f.UserAsync();
        await _f.StaffAsync(gym, admin, GymRole.Admin);

        (await admin.Client.PostAsJsonAsync($"/api/gyms/{gym.Id}/grade-systems",
            new { type = "COLOR", name = "Circuits", values = new[] { new { label = "Pink" } } })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.Client.PostAsJsonAsync($"/api/gyms/{gym.Id}/grade-systems",
            new { type = "CUSTOM", name = "House", values = new[] { new { label = "Easy" }, new { label = "easy" } } })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Editing_values_reorders_adds_and_retires_without_deleting()
    {
        var gym = await _f.GymAsync();
        var admin = await _f.UserAsync();
        await _f.StaffAsync(gym, admin, GymRole.Admin);
        var custom = (await (await admin.Client.PostAsJsonAsync($"/api/gyms/{gym.Id}/grade-systems",
            new { type = "CUSTOM", name = "House", values = new[] { new { label = "Easy" }, new { label = "Medium" }, new { label = "Hard" } } }))
            .ReadAsync<GradeSystemDto>())!;
        var easy = custom.Values.Single(v => v.Label == "Easy");
        var hard = custom.Values.Single(v => v.Label == "Hard");

        var updated = (await (await admin.Client.PutAsJsonAsync($"/api/grade-systems/{custom.Id}/values",
            new { values = new object[] { new { id = easy.Id, label = "Easy" }, new { label = "Very hard" }, new { id = hard.Id, label = "Hard" } } }))
            .ReadAsync<GradeSystemDto>())!;

        updated.Values.Where(v => v.IsActive).Select(v => v.Label).Should().Equal("Easy", "Very hard", "Hard");
        updated.Values.Should().ContainSingle(v => v.Label == "Medium" && !v.IsActive);

        var publicView = await (await _f.CreateClient().GetAsync($"/api/gyms/{gym.Id}/grade-systems")).ReadAsync<List<GradeSystemDto>>();
        publicView!.Single().Values.Should().NotContain(v => v.Label == "Medium");
    }
}
