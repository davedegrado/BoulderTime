using System.Net.Http.Json;
using BoulderTime.Application.Boulders;
using BoulderTime.Application.Common;
using BoulderTime.Tests.Climbing;
using BoulderTime.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Tests.Boulders;

/// <summary>New boulders: set in the last week and still on the wall, marked as such and listed on their own on request.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class NewBouldersTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private async Task<PagedResult<BoulderSummaryDto>> ListAsync(Guid gymId, string query = "") =>
        (await (await _f.CreateClient().GetAsync($"/api/gyms/{gymId}/boulders{query}")).ReadAsync<PagedResult<BoulderSummaryDto>>())!;

    [Fact]
    public async Task A_boulder_set_this_week_is_new_and_the_list_can_show_only_those()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var fresh = await w.BoulderAsync("6A");
        var old = await w.BoulderAsync("6B");
        await _f.Db(db => db.Database.ExecuteSqlRawAsync(
            "UPDATE bouldertime.boulders SET created_at = now() - interval '8 days' WHERE id = {0}", old.Id));

        var all = await ListAsync(w.Gym.Id);
        all.Items.Should().HaveCount(2);
        all.Items.Single(b => b.Id == fresh.Id).IsNew.Should().BeTrue();
        all.Items.Single(b => b.Id == old.Id).IsNew.Should().BeFalse();

        var onlyNew = await ListAsync(w.Gym.Id, "?isNew=true");
        onlyNew.Items.Select(b => b.Id).Should().Equal(fresh.Id);
        onlyNew.Total.Should().Be(1);
        (await ListAsync(w.Gym.Id, $"?isNew=true&sectorId={w.Sector.Id}")).Total.Should().Be(1);
        (await ListAsync(w.Gym.Id, "?isNew=false")).Total.Should().Be(2);
    }

    [Fact]
    public async Task A_new_boulder_taken_down_is_new_no_more()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var fresh = await w.BoulderAsync("6A");
        (await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders/remove", new { boulderIds = new[] { fresh.Id } })).EnsureSuccessStatusCode();

        (await ListAsync(w.Gym.Id, "?status=REMOVED")).Items.Single().IsNew.Should().BeFalse();
        (await ListAsync(w.Gym.Id, "?status=REMOVED&isNew=true")).Total.Should().Be(0);
    }
}
