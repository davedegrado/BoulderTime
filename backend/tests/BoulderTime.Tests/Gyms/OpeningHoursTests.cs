using System.Net;
using System.Net.Http.Json;
using BoulderTime.Application.Gyms;
using BoulderTime.Domain.Staff;
using BoulderTime.Tests.Infrastructure;

namespace BoulderTime.Tests.Gyms;

[Collection(DatabaseCollection.Name)]
public sealed class OpeningHoursTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private static DateOnly Today => DateOnly.FromDateTime(DateTime.UtcNow);

    private static object Hours(string opens = "16:00", string closes = "23:00") => new
    {
        week = new object[]
        {
            new { day = 6, intervals = new[] { new { opens = "10:00", closes = "20:00" } } },
            new { day = 1, intervals = new[] { new { opens = "18:30", closes = "23:00" }, new { opens = "12:00", closes = "14:00" } } },
            new { day = 2, intervals = new[] { new { opens, closes } } },
            new { day = 7, intervals = Array.Empty<object>() },
        },
        closures = new object[]
        {
            new { from = Today.AddDays(10), to = Today.AddDays(12), note = " Lavori " },
            new { from = Today.AddDays(-20), to = Today.AddDays(-15), note = (string?)null },
        },
    };

    [Fact]
    public async Task Admins_set_the_week_and_closures_and_everyone_reads_them_tidied()
    {
        var gym = await _f.GymAsync();
        var admin = await _f.UserAsync();
        await _f.StaffAsync(gym, admin, GymRole.Admin);

        (await admin.Client.PutAsJsonAsync($"/api/gyms/{gym.Id}/opening-hours", Hours())).EnsureSuccessStatusCode();

        var hours = (await (await _f.CreateClient().GetAsync($"/api/gyms/{gym.Slug}")).ReadAsync<GymDetailDto>())!.OpeningHours!;
        hours.Week.Select(d => d.Day).Should().Equal(1, 2, 6);   // in order; a day with no times is a closed day
        hours.Week[0].Intervals.Select(i => i.Opens).Should().Equal("12:00", "18:30");
        hours.Closures.Should().ContainSingle().Which.Should().Be(new ClosureDto(Today.AddDays(10), Today.AddDays(12), "Lavori"));

        // Nothing open and no closures clears them.
        (await admin.Client.PutAsJsonAsync($"/api/gyms/{gym.Id}/opening-hours", new { week = Array.Empty<object>(), closures = Array.Empty<object>() }))
            .StatusCode.Should().BeOneOf(HttpStatusCode.OK, HttpStatusCode.NoContent);
        (await (await _f.CreateClient().GetAsync($"/api/gyms/{gym.Slug}")).ReadAsync<GymDetailDto>())!.OpeningHours.Should().BeNull();
    }

    [Theory]
    [InlineData("25:00", "23:00")]
    [InlineData("9:00", "23:00")]
    [InlineData("20:00", "18:00")]
    [InlineData("13:00", "19:00")] // a valid one, to show the others fail for their times alone
    public async Task Times_must_be_real_and_in_order(string opens, string closes)
    {
        var gym = await _f.GymAsync();
        var admin = await _f.UserAsync();
        await _f.StaffAsync(gym, admin, GymRole.Admin);
        var res = await admin.Client.PutAsJsonAsync($"/api/gyms/{gym.Id}/opening-hours", Hours(opens, closes));
        res.StatusCode.Should().Be(opens == "13:00" ? HttpStatusCode.OK : HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Overlapping_times_on_one_day_are_refused_and_staff_below_admin_cannot_change_them()
    {
        var gym = await _f.GymAsync();
        var admin = await _f.UserAsync();
        var staff = await _f.UserAsync();
        await _f.StaffAsync(gym, admin, GymRole.Admin);
        await _f.StaffAsync(gym, staff, GymRole.Staff);
        var overlap = new { week = new[] { new { day = 3, intervals = new[] { new { opens = "10:00", closes = "15:00" }, new { opens = "14:00", closes = "20:00" } } } } };
        (await admin.Client.PutAsJsonAsync($"/api/gyms/{gym.Id}/opening-hours", overlap)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await staff.Client.PutAsJsonAsync($"/api/gyms/{gym.Id}/opening-hours", Hours())).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
