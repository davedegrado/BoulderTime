using System.Net;
using System.Net.Http.Json;
using BoulderTime.Application.Boulders;
using BoulderTime.Application.Climbing;
using BoulderTime.Application.Common;
using BoulderTime.Application.Notifications;
using BoulderTime.Tests.Climbing;
using BoulderTime.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Tests.Notifications;

/// <summary>"I'll be there" on a gym's events: once per person, events only, names for the gym's followers.</summary>
[Collection(DatabaseCollection.Name)]
public sealed class AttendanceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private static async Task<AnnouncementDto> PostAsync(ClimbingWorld w, string type = "EVENT", DateTimeOffset? date = null)
    {
        var res = await w.Staff.Client.PostAsJsonAsync($"/api/gyms/{w.Gym.Id}/announcements",
            new { type, title = "Serata boulder", content = "Vi aspettiamo venerdì.", eventDate = date ?? DateTimeOffset.UtcNow.AddDays(3) });
        res.EnsureSuccessStatusCode();
        return (await res.ReadAsync<AnnouncementDto>())!;
    }

    private static async Task<AttendanceDto> AttendAsync(TestUser u, Guid id)
    {
        var res = await u.Client.PutAsync($"/api/announcements/{id}/attendance", null);
        res.EnsureSuccessStatusCode();
        return (await res.ReadAsync<AttendanceDto>())!;
    }

    private static async Task<AnnouncementDto> AsSeenBy(HttpClient client, Guid gymId, Guid id) =>
        (await (await client.GetAsync($"/api/gyms/{gymId}/announcements")).ReadAsync<PagedResult<AnnouncementDto>>())!.Items.Single(a => a.Id == id);

    [Fact]
    public async Task Saying_it_twice_is_still_once_and_it_can_be_taken_back()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var e = await PostAsync(w);
        e.TakesAttendance.Should().BeTrue();
        var me = await _f.UserAsync();

        (await AttendAsync(me, e.Id)).Should().Be(new AttendanceDto(1, true));
        (await AttendAsync(me, e.Id)).Should().Be(new AttendanceDto(1, true));
        (await _f.Db(db => db.AnnouncementAttendances.CountAsync())).Should().Be(1);

        var seen = await AsSeenBy(me.Client, w.Gym.Id, e.Id);
        (seen.Attendees, seen.ViewerAttending).Should().Be((1, true));
        (await AsSeenBy(_f.CreateClient(), w.Gym.Id, e.Id)).ViewerAttending.Should().BeFalse();

        var back = await (await me.Client.DeleteAsync($"/api/announcements/{e.Id}/attendance")).ReadAsync<AttendanceDto>();
        back.Should().Be(new AttendanceDto(0, false));
        (await me.Client.DeleteAsync($"/api/announcements/{e.Id}/attendance")).StatusCode.Should().Be(HttpStatusCode.OK);
        (await _f.CreateClient().PutAsync($"/api/announcements/{e.Id}/attendance", null)).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Only_events_still_to_come_take_names()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var me = await _f.UserAsync();
        var competition = await PostAsync(w, "COMPETITION");
        var news = await PostAsync(w, "ANNOUNCEMENT");
        var past = await PostAsync(w, "EVENT", DateTimeOffset.UtcNow.AddDays(-3));
        var today = await PostAsync(w, "EVENT", DateTimeOffset.UtcNow.AddHours(-3));

        competition.TakesAttendance.Should().BeFalse();
        news.TakesAttendance.Should().BeFalse();
        (await me.Client.PutAsync($"/api/announcements/{competition.Id}/attendance", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await me.Client.PutAsync($"/api/announcements/{news.Id}/attendance", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await me.Client.PutAsync($"/api/announcements/{past.Id}/attendance", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await AttendAsync(me, today.Id)).Attendees.Should().Be(1, "an event dated earlier today is still on");
    }

    [Fact]
    public async Task The_gyms_followers_and_staff_see_who_is_coming_and_nobody_else_does()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var e = await PostAsync(w);
        var anna = await _f.UserAsync();
        var follower = await _f.UserAsync();
        var stranger = await _f.UserAsync();
        await AttendAsync(anna, e.Id);
        (await follower.Client.PutAsJsonAsync($"/api/gyms/{w.Gym.Id}/follow", new { })).EnsureSuccessStatusCode();

        var names = await (await follower.Client.GetAsync($"/api/announcements/{e.Id}/attendees")).ReadAsync<List<PersonDto>>();
        names!.Select(p => p.UserId).Should().Equal(anna.Id);
        (await AsSeenBy(follower.Client, w.Gym.Id, e.Id)).CanSeeAttendees.Should().BeTrue();
        (await w.Staff.Client.GetAsync($"/api/announcements/{e.Id}/attendees")).StatusCode.Should().Be(HttpStatusCode.OK);

        (await stranger.Client.GetAsync($"/api/announcements/{e.Id}/attendees")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await AsSeenBy(stranger.Client, w.Gym.Id, e.Id)).CanSeeAttendees.Should().BeFalse();
        (await _f.CreateClient().GetAsync($"/api/announcements/{e.Id}/attendees")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Blocking hides people here as everywhere: the list leaves them out, both ways.
        (await anna.Client.PutAsJsonAsync($"/api/users/me/blocks/{follower.Id}", new { })).EnsureSuccessStatusCode();
        (await (await follower.Client.GetAsync($"/api/announcements/{e.Id}/attendees")).ReadAsync<List<PersonDto>>())!.Should().BeEmpty();
    }

    [Fact]
    public async Task An_update_taken_off_Home_stays_on_the_gyms_page_and_can_come_back()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var e = await PostAsync(w, "ANNOUNCEMENT");
        var me = await _f.UserAsync();
        (await me.Client.PutAsJsonAsync($"/api/gyms/{w.Gym.Id}/follow", new { })).EnsureSuccessStatusCode();
        async Task<List<Guid>> HomeUpdates() => (await (await me.Client.GetAsync("/api/users/me/home")).ReadAsync<HomeDto>())!.Updates.Select(a => a.Id).ToList();
        (await HomeUpdates()).Should().Contain(e.Id);

        (await me.Client.PutAsync($"/api/announcements/{e.Id}/hidden", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await me.Client.PutAsync($"/api/announcements/{e.Id}/hidden", null)).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await HomeUpdates()).Should().NotContain(e.Id);
        (await AsSeenBy(me.Client, w.Gym.Id, e.Id)).Id.Should().Be(e.Id, "the gym's page still lists it");
        var other = await _f.UserAsync();
        (await other.Client.PutAsJsonAsync($"/api/gyms/{w.Gym.Id}/follow", new { })).EnsureSuccessStatusCode();
        (await (await other.Client.GetAsync("/api/users/me/home")).ReadAsync<HomeDto>())!.Updates.Should().Contain(a => a.Id == e.Id, "hiding is one person's");

        (await me.Client.DeleteAsync($"/api/announcements/{e.Id}/hidden")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await HomeUpdates()).Should().Contain(e.Id);
    }

    [Fact]
    public async Task Deleting_the_event_takes_its_list_with_it()
    {
        var w = await ClimbingWorld.CreateAsync(_f);
        var e = await PostAsync(w);
        await AttendAsync(await _f.UserAsync(), e.Id);
        (await w.Staff.Client.DeleteAsync($"/api/announcements/{e.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await _f.Db(db => db.AnnouncementAttendances.CountAsync())).Should().Be(0);
    }
}
