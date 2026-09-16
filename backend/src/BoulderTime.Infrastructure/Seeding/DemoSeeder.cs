using BoulderTime.Application.Abstractions;
using BoulderTime.Domain.Gyms;
using BoulderTime.Domain.Staff;
using BoulderTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Infrastructure.Seeding;

/// <summary>
/// Idempotent demo data so the app is explorable right after setup. Each later phase extends this
/// (grading systems, boulders, attempts, videos, announcements).
/// Demo users can't be created here because accounts live in Supabase Auth; pass an existing
/// user's email to make them OWNER of every demo gym.
/// </summary>
public sealed class DemoSeeder(AppDbContext db, IClock clock)
{
    private static readonly (string Name, string Slug, string City, string Address, string Description, string[] Sectors)[] DemoGyms =
    [
        ("Crimp Factory", "demo-crimp-factory", "Milano", "Via Tortona 31", "Two floors of bouldering in a former textile mill. Steep cave, big slab, and a dedicated kids' wall.", ["Main Hall", "Cave", "Slab", "Kids Wall"]),
        ("Volume Lab", "demo-volume-lab", "Torino", "Corso Regina Margherita 120", "Competition-style setting with big volumes and coordination moves. Resets every week.", ["Comp Wall", "Overhang", "Beginners", "Room 2"]),
        ("Sloper Social", "demo-sloper-social", "Bologna", "Via del Pratello 8", "A community gym with a café, a training board area and a relaxed atmosphere.", ["Front Room", "Training Boards", "Slab Corner"]),
        ("Granite House", "demo-granite-house", "Trento", "Via Brennero 64", "Mountain-town gym with setting inspired by the Dolomites. Great for technical climbers.", ["Arena", "Roof", "Vertical"]),
    ];

    public async Task<string> SeedAsync(string? ownerEmail, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var created = 0;
        foreach (var d in DemoGyms)
        {
            var gym = await db.Gyms.FirstOrDefaultAsync(g => g.Slug == d.Slug, ct);
            if (gym is null)
            {
                gym = Gym.Create(d.Name, d.Slug, d.City);
                gym.UpdateProfile(d.Name, d.Description, d.Address, d.City, $"https://example.com/{d.Slug}", $"info@{d.Slug}.example.com", "+39 02 0000 0000");
                gym.SetStatus(GymStatus.Active);
                db.Gyms.Add(gym);
                for (var i = 0; i < d.Sectors.Length; i++)
                    db.Sectors.Add(Sector.Create(gym.Id, d.Sectors[i], null, i));
                created++;
            }
        }
        await db.SaveChangesAsync(ct);

        var ownerNote = "";
        if (!string.IsNullOrWhiteSpace(ownerEmail))
        {
            var email = ownerEmail.Trim().ToLowerInvariant();
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct)
                       ?? throw new InvalidOperationException($"No user with email {email}. Sign in to BoulderTime once first.");
            var slugs = DemoGyms.Select(g => g.Slug).ToList();
            var gymIds = await db.Gyms.Where(g => slugs.Contains(g.Slug)).Select(g => g.Id).ToListAsync(ct);
            foreach (var gymId in gymIds)
            {
                if (!await db.GymStaff.AnyAsync(s => s.GymId == gymId && s.UserId == user.Id, ct))
                    db.GymStaff.Add(GymStaffMember.Create(gymId, user.Id, GymRole.Owner, null, now));
            }
            await db.SaveChangesAsync(ct);
            ownerNote = $" {email} is OWNER of every demo gym.";
        }
        return $"Seeded {created} new demo gym(s).{ownerNote}";
    }

    /// <summary>
    /// LOCAL DEVELOPMENT ONLY (the CLI refuses outside Development): makes every existing user a platform admin
    /// and OWNER of every demo gym, so a freshly registered account can explore all areas immediately.
    /// </summary>
    public async Task<int> PromoteAllForLocalDevelopmentAsync(CancellationToken ct = default)
    {
        var users = await db.Users.ToListAsync(ct);
        foreach (var user in users) user.GrantPlatformAdmin();
        await db.SaveChangesAsync(ct);
        foreach (var user in users) await SeedAsync(user.Email, ct);
        return users.Count;
    }
}
