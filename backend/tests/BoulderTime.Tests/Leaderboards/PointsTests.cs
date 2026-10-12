using System.Net;
using System.Net.Http.Json;
using BoulderTime.Application.Boulders;
using BoulderTime.Application.Common;
using BoulderTime.Application.Grading;
using BoulderTime.Application.Leaderboards;
using BoulderTime.Tests.Climbing;
using BoulderTime.Tests.Infrastructure;

namespace BoulderTime.Tests.Leaderboards;

/// <summary>
/// Points staff can set by hand, for a grade or for one boulder, and the promise that the card's "+N pt" is always the
/// number the leaderboard adds up. Points are worked out on read, so a change reaches every period at once.
/// </summary>
[Collection(DatabaseCollection.Name)]
public sealed class PointsTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private static async Task Send(TestUser u, Guid boulderId) =>
        (await u.Client.PutAsJsonAsync($"/api/boulders/{boulderId}/attempt", new { attempts = 1, completed = true })).EnsureSuccessStatusCode();

    private async Task<LeaderboardDto> Board(Guid gymId) =>
        (await (await _f.CreateClient().GetAsync($"/api/gyms/{gymId}/leaderboard?metric=POINTS&period=ALL")).ReadAsync<LeaderboardDto>())!;

    private async Task<BoulderDetailDto> Detail(HttpClient client, Guid boulderId) =>
        (await (await client.GetAsync($"/api/boulders/{boulderId}")).ReadAsync<BoulderDetailDto>())!;

    private async Task<GradeSystemDto> System(TestUser staff, Guid gymId, Guid systemId) =>
        (await (await staff.Client.GetAsync($"/api/gyms/{gymId}/grade-systems")).ReadAsync<List<GradeSystemDto>>())!.Single(s => s.Id == systemId);

    /// <summary>The body the editor sends back, with the boulder's current grade and whatever is being changed.</summary>
    private static object Edit(ClimbingWorld w, BoulderDetailDto b, string? name = null, int? points = null) => new
    {
        sectorId = w.Sector.Id, photoPath = b.PhotoPath, holdColor = "BLUE",
        grades = b.Grades.Select(g => new { gradeSystemId = g.GradeSystemId, gradeValueId = g.GradeValueId }).ToArray(),
        name, points,
    };

    [Fact]
    public async Task The_card_shows_what_the_leaderboard_counts_and_a_grades_new_points_reach_every_send_already_made()
    {
        var w = await ClimbingWorld.CreateAsync(_f);   // primary: Fontainebleau, 23 grades
        var b = await w.BoulderAsync("6A");             // rank 5 → 10 + 90·5/22 = 30
        b.Points.Should().Be(30);
        var me = await _f.UserAsync();
        await Send(me, b.Id);
        (await Board(w.Gym.Id)).Entries.Single().Points.Should().Be(30);

        var values = w.Font.Values.Select(v => new { id = (Guid?)v.Id, label = v.Label, points = v.Label == "6A" ? (int?)50 : null }).ToArray();
        (await w.Staff.Client.PutAsJsonAsync($"/api/grade-systems/{w.Font.Id}/values", new { values })).EnsureSuccessStatusCode();

        var sixA = (await System(w.Staff, w.Gym.Id, w.Font.Id)).Values.Single(v => v.Label == "6A");
        (sixA.Points, sixA.AutomaticPoints, sixA.PointsOverride).Should().Be((50, 30, (int?)50));
        (await Detail(_f.CreateClient(), b.Id)).Points.Should().Be(50);
        var list = (await (await _f.CreateClient().GetAsync($"/api/gyms/{w.Gym.Id}/boulders")).ReadAsync<PagedResult<BoulderSummaryDto>>())!;
        list.Items.Single().Points.Should().Be(50);
        var board = await Board(w.Gym.Id);
        board.Entries.Single().Points.Should().Be(50);
        board.Scoring.Should().Be(new ScoringRuleDto(10, 100, true));

        // An edit from an app that doesn't know about points keeps them; 0 goes back to automatic.
        var plain = w.Font.Values.Select(v => new { id = (Guid?)v.Id, label = v.Label }).ToArray();
        (await w.Staff.Client.PutAsJsonAsync($"/api/grade-systems/{w.Font.Id}/values", new { values = plain })).EnsureSuccessStatusCode();
        (await Board(w.Gym.Id)).Entries.Single().Points.Should().Be(50);
        var reset = w.Font.Values.Select(v => new { id = (Guid?)v.Id, label = v.Label, points = (int?)0 }).ToArray();
        (await w.Staff.Client.PutAsJsonAsync($"/api/grade-systems/{w.Font.Id}/values", new { values = reset })).EnsureSuccessStatusCode();
        var after = await Board(w.Gym.Id);
        after.Entries.Single().Points.Should().Be(30);
        after.Scoring!.Customised.Should().BeFalse();
    }

    [Fact]
    public async Task A_boulders_own_points_win_over_its_grade_until_they_are_reset()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var b = await w.BoulderAsync("6A");
        var me = await _f.UserAsync();
        await Send(me, b.Id);

        (await w.Staff.Client.PutAsJsonAsync($"/api/boulders/{b.Id}", Edit(w, b, points: 120))).EnsureSuccessStatusCode();
        (await Detail(_f.CreateClient(), b.Id)).Points.Should().Be(120);
        (await Detail(_f.CreateClient(), b.Id)).PointsOverride.Should().BeNull("only the gym's staff see what was set by hand");
        (await Detail(w.Staff.Client, b.Id)).PointsOverride.Should().Be(120);
        (await Board(w.Gym.Id)).Entries.Single().Points.Should().Be(120);

        // Saved again without points (an older app): they stay.
        (await w.Staff.Client.PutAsJsonAsync($"/api/boulders/{b.Id}", Edit(w, b))).EnsureSuccessStatusCode();
        (await Board(w.Gym.Id)).Entries.Single().Points.Should().Be(120);

        (await w.Staff.Client.PutAsJsonAsync($"/api/boulders/{b.Id}", Edit(w, b, points: 0))).EnsureSuccessStatusCode();
        (await Detail(_f.CreateClient(), b.Id)).Points.Should().Be(30);
        (await Board(w.Gym.Id)).Entries.Single().Points.Should().Be(30);

        (await w.Staff.Client.PutAsJsonAsync($"/api/boulders/{b.Id}", Edit(w, b, points: 1001))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_retired_grade_keeps_its_points_and_the_rest_of_the_scale_still_runs_from_10_to_100()
    {
        var w = await ClimbingWorld.CreateAsync(_f);   // Colour: White, Yellow, Green, Blue, Red, Black → 10, 28, 46, 64, 82, 100
        var colour = w.Color.Values.OrderBy(v => v.Rank).ToList();
        colour.Select(v => v.Points).Should().Equal(10, 28, 46, 64, 82, 100);

        var withoutGreen = colour.Where(v => v.Label != "Green").Select(v => new { id = (Guid?)v.Id, label = v.Label, colorHex = v.ColorHex }).ToArray();
        (await w.Staff.Client.PutAsJsonAsync($"/api/grade-systems/{w.Color.Id}/values", new { values = withoutGreen })).EnsureSuccessStatusCode();

        var after = (await System(w.Staff, w.Gym.Id, w.Color.Id)).Values;
        after.Single(v => v.Label == "Green").Should().Match<GradeValueDto>(v => !v.IsActive && v.Points == 46);
        after.Where(v => v.IsActive).OrderBy(v => v.Rank).Select(v => v.Points).Should().Equal(10, 33, 55, 78, 100);

        // Back in the scale, it is valued by its place again.
        var withGreen = colour.Select(v => new { id = (Guid?)v.Id, label = v.Label, colorHex = v.ColorHex }).ToArray();
        (await w.Staff.Client.PutAsJsonAsync($"/api/grade-systems/{w.Color.Id}/values", new { values = withGreen })).EnsureSuccessStatusCode();
        (await System(w.Staff, w.Gym.Id, w.Color.Id)).Values.OrderBy(v => v.Rank).Select(v => v.Points).Should().Equal(10, 28, 46, 64, 82, 100);
    }

    [Fact]
    public async Task Two_colour_grades_keep_their_second_colour_unless_it_is_cleared()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var colour = w.Color.Values.OrderBy(v => v.Rank).ToList();
        object Value(GradeValueDto v, string? second) => new { id = (Guid?)v.Id, label = v.Label, colorHex = v.ColorHex, colorHex2 = second };

        // A grade between green and blue, inserted in its place.
        var values = new List<object>();
        foreach (var v in colour)
        {
            values.Add(Value(v, null));
            if (v.Label == "Green") values.Add(new { id = (Guid?)null, label = "Green/Blue", colorHex = "#2E9E4F", colorHex2 = "#2f6fdb" });
        }
        (await w.Staff.Client.PutAsJsonAsync($"/api/grade-systems/{w.Color.Id}/values", new { values })).EnsureSuccessStatusCode();
        var duo = (await System(w.Staff, w.Gym.Id, w.Color.Id)).Values.Single(v => v.Label == "Green/Blue");
        (duo.Rank, duo.ColorHex, duo.ColorHex2).Should().Be((3, "#2E9E4F", "#2F6FDB"));

        var b = await w.BoulderAsync("6A", color: null);
        var body = new
        {
            sectorId = w.Sector.Id, photoPath = b.PhotoPath, holdColor = "BLUE",
            grades = new[] { new { gradeSystemId = w.Font.Id, gradeValueId = b.Grades.Single().GradeValueId }, new { gradeSystemId = w.Color.Id, gradeValueId = duo.Id } },
        };
        (await w.Staff.Client.PutAsJsonAsync($"/api/boulders/{b.Id}", body)).EnsureSuccessStatusCode();
        (await Detail(_f.CreateClient(), b.Id)).Grades.Single(g => g.GradeSystemId == w.Color.Id).ColorHex2.Should().Be("#2F6FDB");

        // Saved by an app that doesn't send the second colour: it stays. An empty one clears it.
        var current = (await System(w.Staff, w.Gym.Id, w.Color.Id)).Values.Where(v => v.IsActive).OrderBy(v => v.Rank).ToList();
        (await w.Staff.Client.PutAsJsonAsync($"/api/grade-systems/{w.Color.Id}/values",
            new { values = current.Select(v => new { id = (Guid?)v.Id, label = v.Label, colorHex = v.ColorHex }).ToArray() })).EnsureSuccessStatusCode();
        (await System(w.Staff, w.Gym.Id, w.Color.Id)).Values.Single(v => v.Id == duo.Id).ColorHex2.Should().Be("#2F6FDB");
        (await w.Staff.Client.PutAsJsonAsync($"/api/grade-systems/{w.Color.Id}/values",
            new { values = current.Select(v => Value(v, v.Id == duo.Id ? "" : null)).ToArray() })).EnsureSuccessStatusCode();
        (await System(w.Staff, w.Gym.Id, w.Color.Id)).Values.Single(v => v.Id == duo.Id).ColorHex2.Should().BeNull();

        // Two colours only on colour scales, and two different ones.
        var sameTwice = current.Select(v => Value(v, v.Id == duo.Id ? v.ColorHex : null)).ToArray();
        (await w.Staff.Client.PutAsJsonAsync($"/api/grade-systems/{w.Color.Id}/values", new { values = sameTwice })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var font = w.Font.Values.Select(v => new { id = (Guid?)v.Id, label = v.Label, colorHex2 = v.Label == "6A" ? "#2E9E4F" : null }).ToArray();
        (await w.Staff.Client.PutAsJsonAsync($"/api/grade-systems/{w.Font.Id}/values", new { values = font })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task A_boulder_name_is_optional_trimmed_never_empty_and_kept_by_edits_that_dont_send_it()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var b = await w.BoulderAsync("6A");
        b.Name.Should().BeNull();

        (await w.Staff.Client.PutAsJsonAsync($"/api/boulders/{b.Id}", Edit(w, b, name: "  La Fessura "))).EnsureSuccessStatusCode();
        (await Detail(_f.CreateClient(), b.Id)).Name.Should().Be("La Fessura");
        var list = (await (await _f.CreateClient().GetAsync($"/api/gyms/{w.Gym.Id}/boulders")).ReadAsync<PagedResult<BoulderSummaryDto>>())!;
        list.Items.Single().Name.Should().Be("La Fessura");

        (await w.Staff.Client.PutAsJsonAsync($"/api/boulders/{b.Id}", Edit(w, b))).EnsureSuccessStatusCode();
        (await Detail(_f.CreateClient(), b.Id)).Name.Should().Be("La Fessura");

        (await w.Staff.Client.PutAsJsonAsync($"/api/boulders/{b.Id}", Edit(w, b, name: "   "))).EnsureSuccessStatusCode();
        (await Detail(_f.CreateClient(), b.Id)).Name.Should().BeNull();

        (await w.Staff.Client.PutAsJsonAsync($"/api/boulders/{b.Id}", Edit(w, b, name: new string('x', 61)))).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var climber = await _f.UserAsync();
        (await climber.Client.PutAsJsonAsync($"/api/boulders/{b.Id}", Edit(w, b, name: "Mio"))).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task An_ungraded_boulder_scores_nothing_until_staff_give_it_points()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var b = await w.BoulderAsync("6A");
        var ungraded = new { sectorId = w.Sector.Id, photoPath = b.PhotoPath, holdColor = "BLUE", grades = Array.Empty<object>() };
        (await w.Staff.Client.PutAsJsonAsync($"/api/boulders/{b.Id}", ungraded)).EnsureSuccessStatusCode();
        (await Detail(_f.CreateClient(), b.Id)).Points.Should().BeNull();

        var me = await _f.UserAsync();
        await Send(me, b.Id);
        (await Board(w.Gym.Id)).Entries.Single().Points.Should().Be(0);

        (await w.Staff.Client.PutAsJsonAsync($"/api/boulders/{b.Id}", new { sectorId = w.Sector.Id, photoPath = b.PhotoPath, holdColor = "BLUE", grades = Array.Empty<object>(), points = 40 }))
            .EnsureSuccessStatusCode();
        (await Board(w.Gym.Id)).Entries.Single().Points.Should().Be(40);
    }
}
