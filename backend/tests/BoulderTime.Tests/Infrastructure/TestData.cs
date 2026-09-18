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
    public static async Task<TestUser> UserAsync(this ApiFactory f, string? email = null, bool platformAdmin = false, string language = "en")
    {
        var id = Guid.NewGuid();
        email ??= $"user-{id:N}@example.com";
        var client = f.ClientFor(id, email, language: language);
        (await client.GetAsync("/api/users/me")).EnsureSuccessStatusCode(); // provisions the user
        if (platformAdmin)
        {
            await f.WithDbAsync(async db =>
            {
                var u = await db.Users.FirstAsync(x => x.Id == id);
                u.GrantPlatformAdmin();
                return await db.SaveChangesAsync();
            });
        }
        return new TestUser(id, email.ToLowerInvariant(), client);
    }

    public static Task<Gym> GymAsync(this ApiFactory f, GymStatus status = GymStatus.Active, string? name = null, string city = "Milano") =>
        f.WithDbAsync(async db =>
        {
            name ??= $"Gym {Guid.NewGuid():N}"[..12];
            var gym = Gym.Create(name, BoulderTime.Domain.Common.Slug.From(name) + "-" + Guid.NewGuid().ToString("N")[..6], city);
            gym.SetStatus(status);
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
