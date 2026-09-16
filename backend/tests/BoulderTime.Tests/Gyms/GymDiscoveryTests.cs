using System.Net;
using System.Net.Http.Json;
using BoulderTime.Application.Common;
using BoulderTime.Application.Gyms;
using BoulderTime.Domain.Gyms;
using BoulderTime.Domain.Staff;
using BoulderTime.Tests.Infrastructure;

namespace BoulderTime.Tests.Gyms;

[Collection(DatabaseCollection.Name)]
public sealed class GymDiscoveryTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    [Fact]
    public async Task Anonymous_search_returns_only_active_gyms_matching_name_or_city()
    {
        await _f.GymAsync(GymStatus.Active, "Crimp Factory", "Milano");
        await _f.GymAsync(GymStatus.Active, "Volume Lab", "Torino");
        await _f.GymAsync(GymStatus.Draft, "Secret Milano Gym", "Milano");
        await _f.GymAsync(GymStatus.Archived, "Old Milano Gym", "Milano");
        var anon = _f.CreateClient();

        var byCity = await (await anon.GetAsync("/api/gyms?q=milano")).ReadAsync<PagedResult<GymSummaryDto>>();
        var byName = await (await anon.GetAsync("/api/gyms?q=VOLUME")).ReadAsync<PagedResult<GymSummaryDto>>();

        byCity!.Items.Select(g => g.Name).Should().Equal("Crimp Factory");
        byName!.Items.Select(g => g.Name).Should().Equal("Volume Lab");
    }

    [Fact]
    public async Task Search_text_with_like_wildcards_is_matched_literally()
    {
        await _f.GymAsync(GymStatus.Active, "Anything Gym");
        var res = await (await _f.CreateClient().GetAsync("/api/gyms?q=%25")).ReadAsync<PagedResult<GymSummaryDto>>();
        res!.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Draft_gym_is_hidden_from_public_but_visible_to_its_staff_with_role()
    {
        var gym = await _f.GymAsync(GymStatus.Draft);
        var staff = await _f.UserAsync();
        await _f.StaffAsync(gym, staff, GymRole.Staff);
        var outsider = await _f.UserAsync();

        (await _f.CreateClient().GetAsync($"/api/gyms/{gym.Slug}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await outsider.Client.GetAsync($"/api/gyms/{gym.Slug}")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        var seen = await (await staff.Client.GetAsync($"/api/gyms/{gym.Slug}")).ReadAsync<GymDetailDto>();
        seen!.ViewerRole.Should().Be(GymRole.Staff);
    }

    [Fact]
    public async Task Public_sector_list_hides_inactive_sectors_but_staff_see_them()
    {
        var gym = await _f.GymAsync();
        var admin = await _f.UserAsync();
        await _f.StaffAsync(gym, admin, GymRole.Staff);
        var created = await (await admin.Client.PostAsJsonAsync($"/api/gyms/{gym.Id}/sectors", new { name = "Cave" })).ReadAsync<SectorDto>();
        await admin.Client.PostAsJsonAsync($"/api/gyms/{gym.Id}/sectors", new { name = "Slab" });
        (await admin.Client.PatchAsJsonAsync($"/api/sectors/{created!.Id}", new { isActive = false })).EnsureSuccessStatusCode();

        var publicList = await (await _f.CreateClient().GetAsync($"/api/gyms/{gym.Id}/sectors")).ReadAsync<List<SectorDto>>();
        var staffList = await (await admin.Client.GetAsync($"/api/gyms/{gym.Id}/sectors")).ReadAsync<List<SectorDto>>();

        publicList!.Select(s => s.Name).Should().Equal("Slab");
        staffList!.Select(s => s.Name).Should().Equal("Cave", "Slab");
    }
}
