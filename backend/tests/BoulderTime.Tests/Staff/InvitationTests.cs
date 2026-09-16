using System.Net;
using System.Net.Http.Json;
using BoulderTime.Application.Staff;
using BoulderTime.Application.Users;
using BoulderTime.Domain.Staff;
using BoulderTime.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Tests.Staff;

[Collection(DatabaseCollection.Name)]
public sealed class InvitationTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    [Fact]
    public async Task Invited_person_signs_up_later_sees_the_invitation_and_becomes_staff_on_accept()
    {
        var gym = await _f.GymAsync();
        var admin = await _f.UserAsync();
        await _f.StaffAsync(gym, admin, GymRole.Admin);

        var invite = await admin.Client.PostAsJsonAsync($"/api/gyms/{gym.Id}/invitations", new { email = "Marco@Example.com", role = "STAFF" });
        invite.StatusCode.Should().Be(HttpStatusCode.Created);

        var marco = await _f.UserAsync("marco@example.com"); // account created after the invitation
        var me = await (await marco.Client.GetAsync("/api/users/me")).ReadAsync<CurrentUserDto>();
        me!.PendingInvitations.Should().Be(1);
        var mine = await (await marco.Client.GetAsync("/api/users/me/invitations")).ReadAsync<List<MyInvitationDto>>();
        mine.Should().ContainSingle();

        var accepted = await marco.Client.PostAsync($"/api/invitations/{mine![0].Id}/accept", null);
        accepted.StatusCode.Should().Be(HttpStatusCode.OK);

        var after = await (await marco.Client.GetAsync("/api/users/me")).ReadAsync<CurrentUserDto>();
        after!.StaffGyms.Should().ContainSingle(g => g.GymId == gym.Id && g.Role == GymRole.Staff);
        after.PendingInvitations.Should().Be(0);
    }

    [Fact]
    public async Task Someone_else_cannot_accept_an_invitation_addressed_to_another_email()
    {
        var gym = await _f.GymAsync();
        var admin = await _f.UserAsync();
        await _f.StaffAsync(gym, admin, GymRole.Admin);
        var created = await (await admin.Client.PostAsJsonAsync($"/api/gyms/{gym.Id}/invitations", new { email = "invitee@example.com", role = "ADMIN" })).ReadAsync<GymInvitationDto>();
        var intruder = await _f.UserAsync("intruder@example.com");

        (await intruder.Client.PostAsync($"/api/invitations/{created!.Id}/accept", null)).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await _f.Db(db => db.GymStaff.CountAsync(s => s.GymId == gym.Id && s.UserId == intruder.Id))).Should().Be(0);
    }

    [Fact]
    public async Task Users_cannot_add_themselves_to_staff_by_inviting_themselves()
    {
        var gym = await _f.GymAsync();
        var climber = await _f.UserAsync();

        var res = await climber.Client.PostAsJsonAsync($"/api/gyms/{gym.Id}/invitations", new { email = climber.Email, role = "OWNER" });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Admins_cannot_invite_owners_but_owners_can()
    {
        var gym = await _f.GymAsync();
        var admin = await _f.UserAsync();
        var owner = await _f.UserAsync();
        await _f.StaffAsync(gym, admin, GymRole.Admin);
        await _f.StaffAsync(gym, owner, GymRole.Owner);

        (await admin.Client.PostAsJsonAsync($"/api/gyms/{gym.Id}/invitations", new { email = "a@example.com", role = "OWNER" })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await owner.Client.PostAsJsonAsync($"/api/gyms/{gym.Id}/invitations", new { email = "a@example.com", role = "OWNER" })).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Duplicate_open_invitation_conflicts_but_expired_one_can_be_reissued()
    {
        var gym = await _f.GymAsync();
        var admin = await _f.UserAsync();
        await _f.StaffAsync(gym, admin, GymRole.Admin);
        var body = new { email = "dup@example.com", role = "STAFF" };

        (await admin.Client.PostAsJsonAsync($"/api/gyms/{gym.Id}/invitations", body)).StatusCode.Should().Be(HttpStatusCode.Created);
        (await admin.Client.PostAsJsonAsync($"/api/gyms/{gym.Id}/invitations", body)).StatusCode.Should().Be(HttpStatusCode.Conflict);

        await _f.Db(db => db.Database.ExecuteSqlRawAsync("UPDATE bouldertime.staff_invitations SET expires_at = now() - interval '1 day'"));

        (await admin.Client.PostAsJsonAsync($"/api/gyms/{gym.Id}/invitations", body)).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Expired_invitations_cannot_be_accepted()
    {
        var gym = await _f.GymAsync();
        var admin = await _f.UserAsync();
        await _f.StaffAsync(gym, admin, GymRole.Admin);
        var inv = await (await admin.Client.PostAsJsonAsync($"/api/gyms/{gym.Id}/invitations", new { email = "late@example.com", role = "STAFF" })).ReadAsync<GymInvitationDto>();
        var late = await _f.UserAsync("late@example.com");
        await _f.Db(db => db.Database.ExecuteSqlRawAsync("UPDATE bouldertime.staff_invitations SET expires_at = now() - interval '1 minute'"));

        (await late.Client.PostAsync($"/api/invitations/{inv!.Id}/accept", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Revoked_invitation_can_no_longer_be_accepted()
    {
        var gym = await _f.GymAsync();
        var admin = await _f.UserAsync();
        await _f.StaffAsync(gym, admin, GymRole.Admin);
        var inv = await (await admin.Client.PostAsJsonAsync($"/api/gyms/{gym.Id}/invitations", new { email = "gone@example.com", role = "STAFF" })).ReadAsync<GymInvitationDto>();
        var invitee = await _f.UserAsync("gone@example.com");

        (await admin.Client.DeleteAsync($"/api/gyms/{gym.Id}/invitations/{inv!.Id}")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        (await invitee.Client.PostAsync($"/api/invitations/{inv.Id}/accept", null)).StatusCode.Should().Be(HttpStatusCode.Conflict);
    }
}
