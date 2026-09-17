using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Boulders;
using BoulderTime.Domain.Boulders;
using BoulderTime.Domain.Climbing;
using BoulderTime.Domain.Follows;
using BoulderTime.Domain.Grading;
using BoulderTime.Domain.Gyms;
using BoulderTime.Domain.Staff;
using BoulderTime.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Infrastructure.Seeding;

/// <summary>
/// Idempotent demo data so the app is explorable right after setup. Each later phase extends this.
/// Demo users can't be created here because accounts live in Supabase Auth; pass an existing
/// user's email to make them OWNER of every demo gym.
/// </summary>
public sealed class DemoSeeder(AppDbContext db, IClock clock, IObjectStorage storage)
{
    private sealed record DemoGym(string Name, string Slug, string City, string Address, string Description, string[] Sectors, GradeSystemType[] Grading,
        double Latitude = 0, double Longitude = 0);

    private static readonly DemoGym[] DemoGyms =
    [
        new("Crimp Factory", "demo-crimp-factory", "Milano", "Via Tortona 31", "Two floors of bouldering in a former textile mill. Steep cave, big slab, and a dedicated kids' wall.",
            ["Main Hall", "Cave", "Slab", "Kids Wall"], [GradeSystemType.Color, GradeSystemType.Fontainebleau], 45.4526, 9.1623),
        new("Volume Lab", "demo-volume-lab", "Torino", "Corso Regina Margherita 120", "Competition-style setting with big volumes and coordination moves. Resets every week.",
            ["Comp Wall", "Overhang", "Beginners", "Room 2"], [GradeSystemType.Fontainebleau, GradeSystemType.VScale], 45.0781, 7.6696),
        new("Sloper Social", "demo-sloper-social", "Bologna", "Via del Pratello 8", "A community gym with a café, a training board area and a relaxed atmosphere.",
            ["Front Room", "Training Boards", "Slab Corner"], [GradeSystemType.Color], 44.4949, 11.3325),
        new("Granite House", "demo-granite-house", "Trento", "Via Brennero 64", "Mountain-town gym with setting inspired by the Dolomites. Great for technical climbers.",
            ["Arena", "Roof", "Vertical"], [GradeSystemType.Fontainebleau], 46.0833, 11.1165),
    ];

    /// <summary>Embedded illustrations; the file name carries the hold colour drawn in the picture.</summary>
    private static readonly (string Resource, HoldColor Holds)[] Photos =
    [
        ("wall-1-blue.jpg", HoldColor.Blue), ("wall-2-yellow.jpg", HoldColor.Yellow), ("wall-3-red.jpg", HoldColor.Red),
        ("wall-4-green.jpg", HoldColor.Green), ("wall-5-purple.jpg", HoldColor.Purple), ("wall-6-pink.jpg", HoldColor.Pink),
        ("wall-7-orange.jpg", HoldColor.Orange), ("wall-8-black.jpg", HoldColor.Black),
    ];

    private const int ActiveBouldersPerGym = 14;
    private const int RemovedBouldersPerGym = 4;

    public async Task<string> SeedAsync(string? ownerEmail, CancellationToken ct = default)
    {
        var now = clock.UtcNow;
        var created = 0;
        var boulders = 0;
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
                await db.SaveChangesAsync(ct);
            }
            if (gym.Latitude is null && d.Latitude != 0)
            {
                gym.SetLocation(d.Latitude, d.Longitude); // demo gyms created before locations existed get a pin too
                await db.SaveChangesAsync(ct);
            }
            await SeedGradingAsync(gym.Id, d.Grading, ct);
            boulders += await SeedBouldersAsync(gym.Id, ct);
        }

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
        return $"Seeded {created} new demo gym(s) and {boulders} boulder(s).{ownerNote}";
    }

    private async Task SeedGradingAsync(Guid gymId, GradeSystemType[] types, CancellationToken ct)
    {
        if (await db.GradeSystems.AnyAsync(s => s.GymId == gymId, ct)) return;
        for (var i = 0; i < types.Length; i++)
        {
            var system = GradeSystem.Create(gymId, GradePresets.DefaultName(types[i]), types[i], i);
            db.GradeSystems.Add(system);
            var values = GradePresets.For(types[i]);
            for (var r = 0; r < values.Count; r++)
                db.GradeValues.Add(GradeValue.Create(system.Id, values[r].Label, r, values[r].Hex));
        }
        await db.SaveChangesAsync(ct);
    }

    private async Task<int> SeedBouldersAsync(Guid gymId, CancellationToken ct)
    {
        if (await db.Boulders.AnyAsync(b => b.GymId == gymId, ct)) return 0;

        var sectors = await db.Sectors.Where(s => s.GymId == gymId && s.IsActive).OrderBy(s => s.SortOrder).ToListAsync(ct);
        var systems = await db.GradeSystems.Where(s => s.GymId == gymId && s.IsActive).OrderBy(s => s.SortOrder).ToListAsync(ct);
        var systemIds = systems.Select(s => s.Id).ToList();
        var values = await db.GradeValues.Where(v => systemIds.Contains(v.GradeSystemId) && v.IsActive).OrderBy(v => v.Rank).ToListAsync(ct);
        if (sectors.Count == 0 || systems.Count == 0) return 0;

        // Upload each illustration once per gym, under the same prefix real uploads use.
        var photoPaths = new List<(string Path, HoldColor Holds)>();
        var assembly = typeof(DemoSeeder).Assembly;
        foreach (var (resource, holds) in Photos)
        {
            var path = $"{BoulderService.PhotoPrefix(gymId)}demo-{Path.GetFileNameWithoutExtension(resource)}.jpg";
            if (!await storage.ExistsAsync(StorageBuckets.BoulderImages, path, ct))
            {
                await using var stream = assembly.GetManifestResourceStream($"BoulderTime.Infrastructure.Seeding.DemoPhotos.{resource}")
                                         ?? throw new InvalidOperationException($"Missing embedded demo photo {resource}.");
                await storage.PutAsync(StorageBuckets.BoulderImages, path, stream, "image/jpeg", ct);
            }
            photoPaths.Add((path, holds));
        }

        var rnd = new Random(gymId.GetHashCode());
        var now = clock.UtcNow;
        var total = ActiveBouldersPerGym + RemovedBouldersPerGym;
        for (var i = 0; i < total; i++)
        {
            var photo = photoPaths[i % photoPaths.Count];
            var sector = sectors[i % sectors.Count];
            var boulder = Boulder.Create(gymId, sector.Id, photo.Path, photo.Holds, null, null);
            db.Boulders.Add(boulder);

            // One difficulty per boulder, expressed consistently in every system of the gym.
            var difficulty = rnd.NextDouble() * 0.75;
            foreach (var system in systems)
            {
                var scale = values.Where(v => v.GradeSystemId == system.Id).ToList();
                var value = scale[(int)Math.Round(difficulty * (scale.Count - 1))];
                db.BoulderGrades.Add(BoulderGrade.Official(boulder.Id, system.Id, value.Id, null, now));
            }
            if (i >= ActiveBouldersPerGym) boulder.Remove(null, now);
        }
        await db.SaveChangesAsync(ct);
        return total;
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
        foreach (var user in users)
        {
            await SeedAsync(user.Email, ct);
            await SeedActivityAsync(user.Id, ct);
        }
        return users.Count;
    }

    /// <summary>
    /// LOCAL DEVELOPMENT ONLY: gives an account with no climbing history some follows, sends, projects and ratings
    /// on the demo gyms so Home and Activity show real content. Never touches accounts that already have activity.
    /// </summary>
    private async Task SeedActivityAsync(Guid userId, CancellationToken ct)
    {
        if (await db.BoulderAttempts.AnyAsync(a => a.UserId == userId, ct)) return;
        var now = clock.UtcNow;
        var slugs = DemoGyms.Select(g => g.Slug).ToList();
        var gyms = await db.Gyms.Where(g => slugs.Contains(g.Slug)).OrderBy(g => g.Name).ToListAsync(ct);
        foreach (var gym in gyms)
        {
            if (await db.GymFollows.AnyAsync(f => f.UserId == userId && f.GymId == gym.Id, ct)) continue;
            var follow = GymFollow.Create(userId, gym.Id, now);
            follow.Update(isFavorite: gym.Slug == "demo-crimp-factory", notificationsEnabled: true);
            db.GymFollows.Add(follow);
        }

        var crimp = gyms.FirstOrDefault(g => g.Slug == "demo-crimp-factory");
        if (crimp is not null)
        {
            var boulders = await db.Boulders.Where(b => b.GymId == crimp.Id && b.Status == BoulderStatus.Active)
                .OrderBy(b => b.Id).Take(10).ToListAsync(ct);
            for (var i = 0; i < boulders.Count; i++)
            {
                var attempt = BoulderAttempt.Start(userId, boulders[i].Id);
                var sent = i < 6;
                attempt.Record(sent ? 1 + i % 4 : 3 + i, sent, now);
                db.BoulderAttempts.Add(attempt);
                if (sent) db.BoulderRatings.Add(BoulderRating.Create(userId, boulders[i].Id, 3 + i % 3));
            }
        }
        await db.SaveChangesAsync(ct);
    }
}
