using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BoulderTime.Domain.Gyms;
using BoulderTime.Domain.Staff;
using BoulderTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Tests.Infrastructure;

public static class TestJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) },
    };

    public static Task<T?> ReadAsync<T>(this HttpResponseMessage res) => res.Content.ReadFromJsonAsync<T>(Options);
}

/// <summary>Arrange helpers that write straight to the database, so tests only exercise the behaviour under test via HTTP.</summary>
public sealed record TestUser(Guid Id, string Email, HttpClient Client);

public static class TestData
{
    /// <param name="language">The language this person reads the app in: it decides the language of their notifications.</param>
    /// <param name="inLeaderboards">
    /// New accounts start out of the leaderboards; most tests are about people who are in them, so test users join
    /// unless a test says otherwise. <c>LeaderboardOptInTests</c> covers the real default.
    /// </param>
    public static async Task<TestUser> UserAsync(this ApiFactory f, string? email = null, bool platformAdmin = false, string language = "en",
        bool inLeaderboards = true)
    {
        var id = Guid.NewGuid();
        email ??= $"user-{id:N}@example.com";
        var client = f.ClientFor(id, email, language: language);
        (await client.GetAsync("/api/users/me")).EnsureSuccessStatusCode(); // provisions the user
        if (platformAdmin || inLeaderboards)
        {
            await f.WithDbAsync(async db =>
            {
                var u = await db.Users.FirstAsync(x => x.Id == id);
                if (platformAdmin) u.GrantPlatformAdmin();
                if (inLeaderboards) u.SetLeaderboardOptOut(false);
                return await db.SaveChangesAsync();
            });
        }
        return new TestUser(id, email.ToLowerInvariant(), client);
    }

    /// <param name="communityVideos">
    /// Test gyms allow climber videos by default, because most tests are about what happens once a gym has them.
    /// The production default (off until BoulderTime grants it) is covered by <c>VideoAllowanceTests</c>.
    /// </param>
    public static Task<Gym> GymAsync(this ApiFactory f, GymStatus status = GymStatus.Active, string? name = null, string city = "Milano",
        bool communityVideos = true) =>
        f.WithDbAsync(async db =>
        {
            name ??= $"Gym {Guid.NewGuid():N}"[..12];
            var gym = Gym.Create(name, BoulderTime.Domain.Common.Slug.From(name) + "-" + Guid.NewGuid().ToString("N")[..6], city);
            gym.SetStatus(status);
            gym.SetVideoAllowance(communityVideos, officialBetaLimit: null);
            db.Gyms.Add(gym);
            await db.SaveChangesAsync();
            return gym;
        });

    public static Task<GymStaffMember> StaffAsync(this ApiFactory f, Gym gym, TestUser user, GymRole role) =>
        f.WithDbAsync(async db =>
        {
            var m = GymStaffMember.Create(gym.Id, user.Id, role, null, DateTimeOffset.UtcNow);
            db.GymStaff.Add(m);
            await db.SaveChangesAsync();
            return m;
        });

    public static Task<T> Db<T>(this ApiFactory f, Func<AppDbContext, Task<T>> work) => f.WithDbAsync(work);
}
