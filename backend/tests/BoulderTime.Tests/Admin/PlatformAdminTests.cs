using System.Net;
using System.Net.Http.Json;
using BoulderTime.Application.Admin;
using BoulderTime.Application.Candidates;
using BoulderTime.Application.Common;
using BoulderTime.Application.Gyms;
using BoulderTime.Application.Staff;
using BoulderTime.Application.Users;
using BoulderTime.Domain.Candidates;
using BoulderTime.Domain.Gyms;
using BoulderTime.Domain.Staff;
using BoulderTime.Tests.Infrastructure;

namespace BoulderTime.Tests.Admin;

[Collection(DatabaseCollection.Name)]
public sealed class PlatformAdminTests(PostgresFixture postgres) : IAsyncLifetime
{
    private readonly ApiFactory _f = new(postgres);
    public Task InitializeAsync() => _f.ResetDatabaseAsync();
    public async Task DisposeAsync() => await _f.DisposeAsync();

    [Theory]
    [InlineData("GET", "/api/admin/dashboard")]
    [InlineData("GET", "/api/admin/gyms")]
    [InlineData("GET", "/api/admin/gym-candidates")]
    [InlineData("GET", "/api/admin/users")]
    [InlineData("POST", "/api/admin/gyms")]
    public async Task Admin_endpoints_are_forbidden_to_regular_users_and_gym_owners(string method, string url)
    {
        var gym = await _f.GymAsync();
        var gymOwner = await _f.UserAsync();
        await _f.StaffAsync(gym, gymOwner, GymRole.Owner);

        var req = new HttpRequestMessage(new HttpMethod(method), url);
        if (method == "POST") req.Content = JsonContent.Create(new { name = "X gym", city = "Milano" });

        (await gymOwner.Client.SendAsync(req)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Full_onboarding_candidate_to_gym_to_owner_to_public()
    {
        // 1. A climber suggests a gym
        var climber = await _f.UserAsync();
        var submitted = await (await climber.Client.PostAsJsonAsync("/api/gym-candidates",
            new { gymName = "Boulder Café", city = "Genova", officialEmail = "info@boulder-cafe.example", website = "https://boulder-cafe.example" }))
            .ReadAsync<GymCandidateDto>();
        submitted!.Status.Should().Be(GymCandidateStatus.Pending);

        // 2. Admin sees it, marks it contacted, then creates the gym from it
        var admin = await _f.UserAsync(platformAdmin: true);
        var dash = await (await admin.Client.GetAsync("/api/admin/dashboard")).ReadAsync<AdminDashboardDto>();
        dash!.PendingGymCandidates.Should().Be(1);
        (await admin.Client.PutAsJsonAsync($"/api/admin/gym-candidates/{submitted.Id}/status", new { status = "CONTACTED" })).EnsureSuccessStatusCode();

        var gym = await (await admin.Client.PostAsJsonAsync("/api/admin/gyms",
            new { name = "Boulder Café", city = "Genova", candidateId = submitted.Id })).ReadAsync<GymDetailDto>();
        gym!.Status.Should().Be(GymStatus.Draft);
        gym.Slug.Should().Be("boulder-cafe");

        var candidates = await (await admin.Client.GetAsync("/api/admin/gym-candidates?status=ACCEPTED")).ReadAsync<PagedResult<GymCandidateDto>>();
        candidates!.Items.Should().ContainSingle(c => c.Id == submitted.Id && c.GymId == gym.Id);

        // Draft gyms are not public yet
        (await _f.CreateClient().GetAsync($"/api/gyms/{gym.Slug}")).StatusCode.Should().Be(HttpStatusCode.NotFound);

        // 3. Admin invites the first owner, who accepts
        (await admin.Client.PostAsJsonAsync($"/api/admin/gyms/{gym.Id}/owner-invitations", new { email = "owner@boulder-cafe.example" }))
            .StatusCode.Should().Be(HttpStatusCode.Created);
        var owner = await _f.UserAsync("owner@boulder-cafe.example");
        var invitations = await (await owner.Client.GetAsync("/api/users/me/invitations")).ReadAsync<List<MyInvitationDto>>();
        invitations!.Should().ContainSingle(i => i.Role == GymRole.Owner);
        (await owner.Client.PostAsync($"/api/invitations/{invitations![0].Id}/accept", null)).EnsureSuccessStatusCode();

        // 4. Admin activates; the gym is now discoverable
        (await admin.Client.PutAsJsonAsync($"/api/admin/gyms/{gym.Id}/status", new { status = "ACTIVE" })).EnsureSuccessStatusCode();
        var search = await (await _f.CreateClient().GetAsync("/api/gyms?q=genova")).ReadAsync<PagedResult<GymSummaryDto>>();
        search!.Items.Should().ContainSingle(g => g.Id == gym.Id);

        var me = await (await owner.Client.GetAsync("/api/users/me")).ReadAsync<CurrentUserDto>();
        me!.StaffGyms.Should().ContainSingle(g => g.GymId == gym.Id && g.Role == GymRole.Owner);
    }

    [Fact]
    public async Task Duplicate_gym_names_get_unique_slugs()
    {
        var admin = await _f.UserAsync(platformAdmin: true);
        var a = await (await admin.Client.PostAsJsonAsync("/api/admin/gyms", new { name = "Rock Hall", city = "Roma" })).ReadAsync<GymDetailDto>();
        var b = await (await admin.Client.PostAsJsonAsync("/api/admin/gyms", new { name = "Rock Hall", city = "Napoli" })).ReadAsync<GymDetailDto>();

        a!.Slug.Should().Be("rock-hall");
        b!.Slug.Should().Be("rock-hall-2");
    }

    [Fact]
    public async Task Users_see_only_their_own_suggestions_and_are_capped()
    {
        var alice = await _f.UserAsync();
        var bob = await _f.UserAsync();
        for (var i = 0; i < GymCandidateService.MaxOpenPerUser; i++)
            (await alice.Client.PostAsJsonAsync("/api/gym-candidates", new { gymName = $"Gym {i}", city = "Pisa" })).EnsureSuccessStatusCode();

        (await alice.Client.PostAsJsonAsync("/api/gym-candidates", new { gymName = "One too many", city = "Pisa" })).StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await (await bob.Client.GetAsync("/api/users/me/gym-candidates")).ReadAsync<List<GymCandidateDto>>())!.Should().BeEmpty();
    }
}
