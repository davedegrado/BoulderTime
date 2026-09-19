using System.Net;
using System.Net.Http.Json;
using BoulderTime.Application.Climbing;
using BoulderTime.Application.Common;
using BoulderTime.Application.Gyms;
using BoulderTime.Domain.Gyms;
using BoulderTime.Domain.Staff;
using BoulderTime.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Tests.Partners;

[Collection(DatabaseCollection.Name)]
public sealed class PartnerTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    private static Task<HttpResponseMessage> Designate(TestUser user, Gym gym, bool isFounding) =>
        user.Client.PutAsJsonAsync($"/api/admin/gyms/{gym.Id}/founding", new { isFoundingGym = isFounding });

    [Fact]
    public async Task Only_platform_admins_can_hand_out_either_distinction()
    {
        var gym = await _f.GymAsync(GymStatus.Active, "Crimp Factory");
        var owner = await _f.UserAsync();
        var admin = await _f.UserAsync();
        var climber = await _f.UserAsync();
        await _f.StaffAsync(gym, owner, GymRole.Owner);
        await _f.StaffAsync(gym, admin, GymRole.Admin);

        foreach (var user in new[] { owner, admin, climber })
        {
            (await Designate(user, gym, true)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await user.Client.PostAsJsonAsync($"/api/admin/gyms/{gym.Id}/early-partner", new { })).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await user.Client.DeleteAsync($"/api/admin/gyms/{gym.Id}/early-partner")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
            (await user.Client.GetAsync("/api/admin/partners")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        }
        (await _f.CreateClient().PutAsJsonAsync($"/api/admin/gyms/{gym.Id}/founding", new { isFoundingGym = true })).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        (await _f.Db(db => db.Gyms.Where(g => g.Id == gym.Id).Select(g => g.IsFoundingGym).FirstAsync())).Should().BeFalse();
    }

    [Fact]
    public async Task There_can_only_ever_be_one_founding_gym()
    {
        var first = await _f.GymAsync(GymStatus.Active, "Crimp Factory");
        var second = await _f.GymAsync(GymStatus.Active, "Volume Lab");
        var admin = await _f.UserAsync(platformAdmin: true);

        (await Designate(admin, first, true)).EnsureSuccessStatusCode();
        var refused = await Designate(admin, second, true);
        refused.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await refused.Content.ReadAsStringAsync()).Should().Contain("Crimp Factory");

        // Moving the distinction is deliberate: clear it first, then designate the other gym.
        (await Designate(admin, first, false)).EnsureSuccessStatusCode();
        (await Designate(admin, second, true)).EnsureSuccessStatusCode();
        (await _f.Db(db => db.Gyms.CountAsync(g => g.IsFoundingGym))).Should().Be(1);
    }

    [Fact]
    public async Task The_founding_badge_shows_on_the_gym_and_in_search_and_disappears_when_removed()
    {
        var gym = await _f.GymAsync(GymStatus.Active, "Crimp Factory");
        var admin = await _f.UserAsync(platformAdmin: true);
        await Designate(admin, gym, true);

        var detail = (await (await _f.CreateClient().GetAsync($"/api/gyms/{gym.Slug}")).ReadAsync<GymDetailDto>())!;
        detail.IsFoundingGym.Should().BeTrue();
        detail.IsEarlyPartner.Should().BeFalse();
        var search = (await (await _f.CreateClient().GetAsync("/api/gyms?q=Crimp")).ReadAsync<PagedResult<GymSummaryDto>>())!;
        search.Items.Single().IsFoundingGym.Should().BeTrue();

        await Designate(admin, gym, false);
        (await (await _f.CreateClient().GetAsync($"/api/gyms/{gym.Slug}")).ReadAsync<GymDetailDto>())!.IsFoundingGym.Should().BeFalse();
    }

    [Fact]
    public async Task Several_gyms_can_be_early_partners_and_ending_one_keeps_its_history()
    {
        var a = await _f.GymAsync(GymStatus.Active, "Gym A");
        var b = await _f.GymAsync(GymStatus.Active, "Gym B");
        var admin = await _f.UserAsync(platformAdmin: true);

        var started = (await (await admin.Client.PostAsJsonAsync($"/api/admin/gyms/{a.Id}/early-partner", new { note = "Launch partner" })).ReadAsync<EarlyPartnerDto>())!;
        started.IsActive.Should().BeTrue();
        started.EndedAt.Should().BeNull();
        (await admin.Client.PostAsJsonAsync($"/api/admin/gyms/{b.Id}/early-partner", new { })).EnsureSuccessStatusCode();

        var partners = (await (await admin.Client.GetAsync("/api/admin/partners")).ReadAsync<List<PartnerGymDto>>())!;
        partners.Select(p => p.Gym.Name).Should().Equal("Gym A", "Gym B");
        partners.Should().OnlyContain(p => p.Gym.IsEarlyPartner);

        var ended = (await (await admin.Client.DeleteAsync($"/api/admin/gyms/{a.Id}/early-partner")).ReadAsync<EarlyPartnerDto>())!;
        ended.EndedAt.Should().NotBeNull();
        ended.IsActive.Should().BeFalse();
        (await (await _f.CreateClient().GetAsync($"/api/gyms/{a.Slug}")).ReadAsync<GymDetailDto>())!.IsEarlyPartner.Should().BeFalse();
        (await (await _f.CreateClient().GetAsync($"/api/gyms/{b.Slug}")).ReadAsync<GymDetailDto>())!.IsEarlyPartner.Should().BeTrue();
        (await _f.Db(db => db.EarlyPartnerships.CountAsync(p => p.GymId == a.Id))).Should().Be(1); // kept, not deleted
    }

    [Fact]
    public async Task A_gym_can_hold_both_distinctions_at_once()
    {
        var gym = await _f.GymAsync(GymStatus.Active, "Crimp Factory");
        var admin = await _f.UserAsync(platformAdmin: true);
        await Designate(admin, gym, true);
        await admin.Client.PostAsJsonAsync($"/api/admin/gyms/{gym.Id}/early-partner", new { });

        var detail = (await (await _f.CreateClient().GetAsync($"/api/gyms/{gym.Slug}")).ReadAsync<GymDetailDto>())!;
        detail.IsFoundingGym.Should().BeTrue();
        detail.IsEarlyPartner.Should().BeTrue();
        detail.EarlyPartnerSince.Should().NotBeNull();
    }

    [Fact]
    public async Task Distinctions_follow_staff_roles_and_never_followers()
    {
        var gym = await _f.GymAsync(GymStatus.Active, "Crimp Factory");
        var admin = await _f.UserAsync(platformAdmin: true);
        var setter = await _f.UserAsync();
        var fan = await _f.UserAsync();
        await _f.StaffAsync(gym, setter, GymRole.Staff);
        await Designate(admin, gym, true);
        await admin.Client.PostAsJsonAsync($"/api/admin/gyms/{gym.Id}/early-partner", new { });
        await fan.Client.PutAsJsonAsync($"/api/gyms/{gym.Id}/follow", new { });

        async Task<ProfileDto> Profile(TestUser u) => (await (await _f.CreateClient().GetAsync($"/api/users/{u.Id}/profile")).ReadAsync<ProfileDto>())!;

        var staffProfile = await Profile(setter);
        staffProfile.StaffDistinctions.Should().ContainSingle(d => d.IsFoundingGym && d.IsEarlyPartner && d.Role == GymRole.Staff && d.GymName == "Crimp Factory");
        (await Profile(fan)).StaffDistinctions.Should().BeEmpty(); // following is not belonging

        // Leaving the staff removes the badge, with no extra bookkeeping.
        (await setter.Client.DeleteAsync($"/api/gyms/{gym.Id}/staff/{setter.Id}")).EnsureSuccessStatusCode();
        (await Profile(setter)).StaffDistinctions.Should().BeEmpty();
    }

    [Fact]
    public async Task Losing_the_status_removes_the_staff_badge_too()
    {
        var gym = await _f.GymAsync(GymStatus.Active, "Crimp Factory");
        var admin = await _f.UserAsync(platformAdmin: true);
        var owner = await _f.UserAsync();
        await _f.StaffAsync(gym, owner, GymRole.Owner);
        await Designate(admin, gym, true);

        var before = (await (await _f.CreateClient().GetAsync($"/api/users/{owner.Id}/profile")).ReadAsync<ProfileDto>())!;
        before.StaffDistinctions.Should().ContainSingle(d => d.IsFoundingGym);

        await Designate(admin, gym, false);
        var after = (await (await _f.CreateClient().GetAsync($"/api/users/{owner.Id}/profile")).ReadAsync<ProfileDto>())!;
        after.StaffDistinctions.Should().BeEmpty();
    }
}
