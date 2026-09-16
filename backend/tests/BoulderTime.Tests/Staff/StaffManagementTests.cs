using System.Net;
using System.Net.Http.Json;
using BoulderTime.Domain.Staff;
using BoulderTime.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Tests.Staff;

[Collection(DatabaseCollection.Name)]
public sealed class StaffManagementTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    [Fact]
    public async Task The_last_owner_cannot_be_demoted_removed_or_leave()
    {
        var gym = await _f.GymAsync();
        var owner = await _f.UserAsync();
        await _f.StaffAsync(gym, owner, GymRole.Owner);

        (await owner.Client.PatchAsJsonAsync($"/api/gyms/{gym.Id}/staff/{owner.Id}", new { role = "ADMIN" })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await owner.Client.DeleteAsync($"/api/gyms/{gym.Id}/staff/{owner.Id}")).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task With_a_second_owner_an_owner_can_step_down()
    {
        var gym = await _f.GymAsync();
        var a = await _f.UserAsync();
        var b = await _f.UserAsync();
        await _f.StaffAsync(gym, a, GymRole.Owner);
        await _f.StaffAsync(gym, b, GymRole.Owner);

        (await a.Client.PatchAsJsonAsync($"/api/gyms/{gym.Id}/staff/{a.Id}", new { role = "ADMIN" })).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Admins_cannot_demote_or_remove_owners_or_promote_to_owner()
    {
        var gym = await _f.GymAsync();
        var owner = await _f.UserAsync();
        var admin = await _f.UserAsync();
        var staff = await _f.UserAsync();
        await _f.StaffAsync(gym, owner, GymRole.Owner);
        await _f.StaffAsync(gym, admin, GymRole.Admin);
        await _f.StaffAsync(gym, staff, GymRole.Staff);

        (await admin.Client.DeleteAsync($"/api/gyms/{gym.Id}/staff/{owner.Id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.Client.PatchAsJsonAsync($"/api/gyms/{gym.Id}/staff/{staff.Id}", new { role = "OWNER" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await admin.Client.PatchAsJsonAsync($"/api/gyms/{gym.Id}/staff/{staff.Id}", new { role = "ADMIN" })).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Staff_cannot_remove_others_but_can_leave()
    {
        var gym = await _f.GymAsync();
        var owner = await _f.UserAsync();
        var s1 = await _f.UserAsync();
        var s2 = await _f.UserAsync();
        await _f.StaffAsync(gym, owner, GymRole.Owner);
        await _f.StaffAsync(gym, s1, GymRole.Staff);
        await _f.StaffAsync(gym, s2, GymRole.Staff);

        (await s1.Client.DeleteAsync($"/api/gyms/{gym.Id}/staff/{s2.Id}")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await s1.Client.DeleteAsync($"/api/gyms/{gym.Id}/staff/{s1.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _f.Db(db => db.GymStaff.CountAsync(m => m.GymId == gym.Id))).Should().Be(2);
    }
}
