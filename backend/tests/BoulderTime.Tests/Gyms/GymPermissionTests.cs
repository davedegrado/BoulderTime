using System.Net;
using System.Net.Http.Json;
using BoulderTime.Application.Gyms;
using BoulderTime.Domain.Gyms;
using BoulderTime.Domain.Staff;
using BoulderTime.Tests.Infrastructure;

namespace BoulderTime.Tests.Gyms;

[Collection(DatabaseCollection.Name)]
public sealed class GymPermissionTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    [Fact]
    public async Task Non_staff_cannot_create_sectors_or_edit_the_gym()
    {
        var gym = await _f.GymAsync();
        var climber = await _f.UserAsync();

        (await climber.Client.PostAsJsonAsync($"/api/gyms/{gym.Id}/sectors", new { name = "Cave" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await climber.Client.PatchAsJsonAsync($"/api/gyms/{gym.Id}", new { name = "Hijacked", city = "X" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await _f.CreateClient().PostAsJsonAsync($"/api/gyms/{gym.Id}/sectors", new { name = "Cave" })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Staff_role_manages_sectors_but_not_gym_settings()
    {
        var gym = await _f.GymAsync();
        var staff = await _f.UserAsync();
        await _f.StaffAsync(gym, staff, GymRole.Staff);

        (await staff.Client.PostAsJsonAsync($"/api/gyms/{gym.Id}/sectors", new { name = "Cave" })).StatusCode.Should().Be(HttpStatusCode.Created);
        var settings = await staff.Client.PatchAsJsonAsync($"/api/gyms/{gym.Id}", new { name = "New name", city = "Milano" });
        settings.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admin_updates_gym_settings_with_validation()
    {
        var gym = await _f.GymAsync();
        var admin = await _f.UserAsync();
        await _f.StaffAsync(gym, admin, GymRole.Admin);

        var bad = await admin.Client.PatchAsJsonAsync($"/api/gyms/{gym.Id}", new { name = "Crimp Factory", city = "Milano", website = "not a url" });
        bad.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var ok = await (await admin.Client.PatchAsJsonAsync($"/api/gyms/{gym.Id}", new { name = "Crimp Factory", city = "Milano", website = "https://crimp.example" })).ReadAsync<GymDetailDto>();
        ok!.Name.Should().Be("Crimp Factory");
        ok.Website.Should().Be("https://crimp.example");
    }

    [Fact]
    public async Task Staff_of_one_gym_have_no_rights_at_another()
    {
        var mine = await _f.GymAsync();
        var other = await _f.GymAsync();
        var owner = await _f.UserAsync();
        await _f.StaffAsync(mine, owner, GymRole.Owner);

        (await owner.Client.PostAsJsonAsync($"/api/gyms/{other.Id}/sectors", new { name = "Cave" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await owner.Client.GetAsync($"/api/gyms/{other.Id}/staff")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Duplicate_sector_names_in_a_gym_are_rejected_and_reorder_requires_all_sectors()
    {
        var gym = await _f.GymAsync();
        var staff = await _f.UserAsync();
        await _f.StaffAsync(gym, staff, GymRole.Staff);
        var a = await (await staff.Client.PostAsJsonAsync($"/api/gyms/{gym.Id}/sectors", new { name = "Cave" })).ReadAsync<SectorDto>();
        var b = await (await staff.Client.PostAsJsonAsync($"/api/gyms/{gym.Id}/sectors", new { name = "Slab" })).ReadAsync<SectorDto>();

        (await staff.Client.PostAsJsonAsync($"/api/gyms/{gym.Id}/sectors", new { name = "Cave" })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await staff.Client.PutAsJsonAsync($"/api/gyms/{gym.Id}/sectors/order", new { sectorIds = new[] { b!.Id } })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var reordered = await (await staff.Client.PutAsJsonAsync($"/api/gyms/{gym.Id}/sectors/order", new { sectorIds = new[] { b.Id, a!.Id } })).ReadAsync<List<SectorDto>>();
        reordered!.Select(s => s.Name).Should().Equal("Slab", "Cave");
    }
}
