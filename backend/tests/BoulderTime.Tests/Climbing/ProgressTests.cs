using System.Net;
using System.Net.Http.Json;
using BoulderTime.Application.Boulders;
using BoulderTime.Application.Common;
using BoulderTime.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Tests.Climbing;

[Collection(DatabaseCollection.Name)]
public sealed class ProgressTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    [Fact]
    public async Task Tracking_is_one_row_per_user_and_boulder_and_completion_implies_an_attempt()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var b = await w.BoulderAsync();
        var me = await _f.UserAsync();

        var tried = await (await me.Client.PutAsJsonAsync($"/api/boulders/{b.Id}/attempt", new { attempts = 4, completed = false })).ReadAsync<ViewerProgressDto>();
        tried!.Attempts.Should().Be(4);
        tried.Completed.Should().BeFalse();

        var sent = await (await me.Client.PutAsJsonAsync($"/api/boulders/{b.Id}/attempt", new { attempts = 0, completed = true })).ReadAsync<ViewerProgressDto>();
        sent!.Attempts.Should().Be(1);
        sent.Completed.Should().BeTrue();
        sent.CompletedAt.Should().NotBeNull();

        (await _f.Db(db => db.BoulderAttempts.CountAsync(a => a.BoulderId == b.Id))).Should().Be(1);
        var detail = await (await me.Client.GetAsync($"/api/boulders/{b.Id}")).ReadAsync<BoulderDetailDto>();
        detail!.Viewer!.Completed.Should().BeTrue();
    }

    [Fact]
    public async Task Clearing_tracking_deletes_the_row_and_the_dependent_rating()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var b = await w.BoulderAsync();
        var me = await _f.UserAsync();
        await me.Client.PutAsJsonAsync($"/api/boulders/{b.Id}/attempt", new { attempts = 2, completed = false });
        (await me.Client.PutAsJsonAsync($"/api/boulders/{b.Id}/rating", new { rating = 4 })).EnsureSuccessStatusCode();

        var cleared = await me.Client.PutAsJsonAsync($"/api/boulders/{b.Id}/attempt", new { attempts = 0, completed = false });

        cleared.StatusCode.Should().Be(HttpStatusCode.NoContent); // nothing tracked any more
        (await _f.Db(db => db.BoulderAttempts.CountAsync(a => a.BoulderId == b.Id))).Should().Be(0);
        (await _f.Db(db => db.BoulderRatings.CountAsync(r => r.BoulderId == b.Id))).Should().Be(0);
    }

    [Fact]
    public async Task Rating_requires_an_attempt_is_one_per_user_and_averages_for_everyone()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var b = await w.BoulderAsync();
        var alice = await _f.UserAsync();
        var bob = await _f.UserAsync();

        (await alice.Client.PutAsJsonAsync($"/api/boulders/{b.Id}/rating", new { rating = 5 })).StatusCode.Should().Be(HttpStatusCode.Forbidden);

        await alice.Client.PutAsJsonAsync($"/api/boulders/{b.Id}/attempt", new { attempts = 1, completed = true });
        await bob.Client.PutAsJsonAsync($"/api/boulders/{b.Id}/attempt", new { attempts = 3, completed = false });
        (await alice.Client.PutAsJsonAsync($"/api/boulders/{b.Id}/rating", new { rating = 5 })).EnsureSuccessStatusCode();
        (await alice.Client.PutAsJsonAsync($"/api/boulders/{b.Id}/rating", new { rating = 4 })).EnsureSuccessStatusCode(); // change, not add
        (await bob.Client.PutAsJsonAsync($"/api/boulders/{b.Id}/rating", new { rating = 3 })).EnsureSuccessStatusCode();
        (await bob.Client.PutAsJsonAsync($"/api/boulders/{b.Id}/rating", new { rating = 6 })).StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var anon = await (await _f.CreateClient().GetAsync($"/api/boulders/{b.Id}")).ReadAsync<BoulderDetailDto>();
        anon!.Rating.Count.Should().Be(2);
        anon.Rating.Average.Should().Be(3.5);
        anon.Viewer.Should().BeNull();
    }

    [Fact]
    public async Task Completions_survive_removal_and_can_still_be_corrected()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var b = await w.BoulderAsync();
        var me = await _f.UserAsync();
        await me.Client.PutAsJsonAsync($"/api/boulders/{b.Id}/attempt", new { attempts = 3, completed = true });

        await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/boulders/remove", new { boulderIds = new[] { b.Id } });

        var history = await (await me.Client.GetAsync($"/api/users/{me.Id}/history?filter=COMPLETED")).ReadAsync<PagedResult<Application.Climbing.ClimbHistoryItemDto>>();
        history!.Items.Should().ContainSingle(i => i.Boulder.Id == b.Id && i.Attempts == 3 && i.Boulder.Status == Domain.Boulders.BoulderStatus.Removed);

        (await me.Client.PutAsJsonAsync($"/api/boulders/{b.Id}/attempt", new { attempts = 2, completed = true })).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Progress_filters_use_only_the_callers_own_tracking()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var sent = await w.BoulderAsync("6A");
        var project = await w.BoulderAsync("6B");
        var untried = await w.BoulderAsync("6C");
        var me = await _f.UserAsync();
        var other = await _f.UserAsync();
        await me.Client.PutAsJsonAsync($"/api/boulders/{sent.Id}/attempt", new { attempts = 2, completed = true });
        await me.Client.PutAsJsonAsync($"/api/boulders/{project.Id}/attempt", new { attempts = 5, completed = false });
        await other.Client.PutAsJsonAsync($"/api/boulders/{untried.Id}/attempt", new { attempts = 1, completed = true });

        async Task<IEnumerable<Guid>> Ids(string progress) =>
            (await (await me.Client.GetAsync($"/api/gyms/{w.Gym.Id}/boulders?progress={progress}")).ReadAsync<PagedResult<BoulderSummaryDto>>())!.Items.Select(i => i.Id);

        (await Ids("COMPLETED")).Should().Equal(sent.Id);
        (await Ids("PROJECTS")).Should().Equal(project.Id);
        (await Ids("UNTRIED")).Should().Equal(untried.Id);
    }

    [Fact]
    public async Task Anonymous_users_cannot_track_and_invalid_input_is_rejected()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var b = await w.BoulderAsync();
        var me = await _f.UserAsync();

        (await _f.CreateClient().PutAsJsonAsync($"/api/boulders/{b.Id}/attempt", new { attempts = 1, completed = true })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await me.Client.PutAsJsonAsync($"/api/boulders/{b.Id}/attempt", new { attempts = -1, completed = false })).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await me.Client.PutAsJsonAsync($"/api/boulders/{Guid.NewGuid()}/attempt", new { attempts = 1, completed = false })).StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
