using System.Net.Http.Json;
using BoulderTime.Application.Boulders;
using BoulderTime.Application.Common;
using BoulderTime.Domain.Gyms;
using BoulderTime.Tests.Climbing;
using BoulderTime.Tests.Infrastructure;

namespace BoulderTime.Tests.Boulders;

/// <summary>
/// Sorting a gym's boulder list. Without a sort the list keeps the order it always had, which is what the apps
/// already installed rely on.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class BoulderSortTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private async Task<List<Guid>> ListAsync(ClimbingWorld w, string query = "", int pageSize = 50)
    {
        var res = await _f.CreateClient().GetAsync($"/api/gyms/{w.Gym.Id}/boulders?pageSize={pageSize}{query}");
        res.EnsureSuccessStatusCode();
        return (await res.ReadAsync<PagedResult<BoulderSummaryDto>>())!.Items.Select(b => b.Id).ToList();
    }

    private async Task<BoulderDetailDto> BoulderAsync(ClimbingWorld w, Sector sector, string? font)
    {
        var res = await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders", new
        {
            sectorId = sector.Id, photoPath = await w.Staff.UploadPhotoAsync(_f, w.Gym), holdColor = "BLUE",
            grades = font is null ? Array.Empty<object>() : new object[] { new { gradeSystemId = w.Font.Id, gradeValueId = w.Font.Values.Single(v => v.Label == font).Id } },
        });
        res.EnsureSuccessStatusCode();
        return (await res.ReadAsync<BoulderDetailDto>())!;
    }

    /// <summary>Second sector first in the gym's order, so "by sector" can't pass by alphabet or by chance.</summary>
    private async Task<(ClimbingWorld W, Sector First, Sector Second)> SetupAsync()
    {
        var w = await ClimbingWorld.CreateAsync(_f); // w.Sector "Cave", order 0
        var slab = await _f.Db(async db =>
        {
            var s = Sector.Create(w.Gym.Id, "Slab", null, 1);
            db.Sectors.Add(s);
            await db.SaveChangesAsync();
            return s;
        });
        return (w, w.Sector, slab);
    }

    [Fact]
    public async Task Without_a_sort_the_list_keeps_its_old_order_newest_first()
    {
        var (w, cave, slab) = await SetupAsync();
        var a = await BoulderAsync(w, slab, "6A");
        var b = await BoulderAsync(w, cave, "7A");
        var c = await BoulderAsync(w, slab, null);
        (await ListAsync(w)).Should().Equal(c.Id, b.Id, a.Id);
    }

    [Fact]
    public async Task By_sector_follows_the_gyms_order_of_its_sectors_newest_first_inside_each()
    {
        var (w, cave, slab) = await SetupAsync();
        var slab1 = await BoulderAsync(w, slab, "6A");
        var cave1 = await BoulderAsync(w, cave, "7A");
        var slab2 = await BoulderAsync(w, slab, "5");
        var cave2 = await BoulderAsync(w, cave, null);

        (await ListAsync(w, "&sort=SECTOR")).Should().Equal(cave2.Id, cave1.Id, slab2.Id, slab1.Id);
        (await ListAsync(w, "&sort=SECTOR&order=DESC")).Should().Equal(slab2.Id, slab1.Id, cave2.Id, cave1.Id);
    }

    [Fact]
    public async Task By_points_and_by_grade_follow_the_scale_and_put_ungraded_boulders_last_either_way()
    {
        var (w, cave, _) = await SetupAsync();
        var mid = await BoulderAsync(w, cave, "6A");
        var none = await BoulderAsync(w, cave, null);
        var hard = await BoulderAsync(w, cave, "7C+");
        var easy = await BoulderAsync(w, cave, "4");

        (await ListAsync(w, "&sort=POINTS")).Should().Equal(hard.Id, mid.Id, easy.Id, none.Id);
        (await ListAsync(w, "&sort=POINTS&order=ASC")).Should().Equal(easy.Id, mid.Id, hard.Id, none.Id);
        (await ListAsync(w, "&sort=GRADE")).Should().Equal(easy.Id, mid.Id, hard.Id, none.Id);
        (await ListAsync(w, "&sort=GRADE&order=DESC")).Should().Equal(hard.Id, mid.Id, easy.Id, none.Id);
    }

    [Fact]
    public async Task By_date_and_paging_through_a_sorted_list_shows_every_boulder_once()
    {
        var (w, cave, slab) = await SetupAsync();
        var made = new List<Guid>();
        foreach (var (sector, font) in new[] { (slab, "6A"), (cave, "6A"), (slab, "6A"), (cave, (string?)null), (slab, "7A") })
            made.Add((await BoulderAsync(w, sector, font)).Id);

        (await ListAsync(w, "&sort=SET")).Should().Equal(Enumerable.Reverse(made));
        (await ListAsync(w, "&sort=SET&order=ASC")).Should().Equal(made);

        // Ties on points (three at 6A): two at a time, the pages still add up to the whole list.
        var whole = await ListAsync(w, "&sort=POINTS");
        var paged = new List<Guid>();
        for (var page = 1; page <= 3; page++) paged.AddRange(await ListAsync(w, $"&sort=POINTS&page={page}", pageSize: 2));
        paged.Should().Equal(whole);
    }
}
