using BoulderTime.Application.Abstractions;
using BoulderTime.Application.Boulders;
using BoulderTime.Domain.Boulders;
using BoulderTime.Domain.Climbing;
using BoulderTime.Domain.Community;
using BoulderTime.Domain.Notifications;
using BoulderTime.Domain.Users;
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
        new("Crimp Factory", "demo-crimp-factory", "Milano", "Via Tortona 31",
            "Due piani di boulder in un ex cotonificio. Grotta strapiombante, placca grande e una parete dedicata ai bambini.",
            ["Sala Grande", "Grotta", "Placca", "Parete Bambini"], [GradeSystemType.Color, GradeSystemType.Fontainebleau], 45.4526, 9.1623),
        new("Volume Lab", "demo-volume-lab", "Torino", "Corso Regina Margherita 120",
            "Tracciature da gara con volumi grandi e movimenti di coordinazione. Si ritraccia ogni settimana.",
            ["Parete Gara", "Strapiombo", "Principianti", "Sala 2"], [GradeSystemType.Fontainebleau, GradeSystemType.VScale], 45.0781, 7.6696),
        new("Sloper Social", "demo-sloper-social", "Bologna", "Via del Pratello 8",
            "Palestra di quartiere con bar, zona pan Güllich e atmosfera rilassata.",
            ["Sala Davanti", "Pan Güllich", "Angolo Placca"], [GradeSystemType.Color], 44.4949, 11.3325),
        new("Granite House", "demo-granite-house", "Trento", "Via Brennero 64",
            "Palestra di montagna con tracciature ispirate alle Dolomiti. Ottima per chi ama il tecnico.",
            ["Arena", "Tetto", "Verticale"], [GradeSystemType.Fontainebleau], 46.0833, 11.1165),
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
            await RenameLegacySectorsAsync(gym.Id, d.Sectors, ct);
            await SeedGradingAsync(gym.Id, d.Grading, ct);
            boulders += await SeedBouldersAsync(gym.Id, ct);
        }

        var climbers = await SeedDemoClimbersAsync(ct);
        await SeedCommunityAsync(climbers, ct);

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
        return $"Seeded {created} new demo gym(s), {boulders} boulder(s) and demo climbers with history, comments and updates.{ownerNote}";
    }

    /// <summary>
    /// Demo climbers. They are BoulderTime profiles without a Supabase Auth account: nobody can sign in as them,
    /// they only make the app look alive (history, ratings, comments, leaderboards). Their email domain marks them
    /// so they can be removed in one query.
    /// </summary>
    public const string DemoEmailDomain = "demo.bouldertime.invalid";

    private static readonly (string Name, string Email)[] DemoClimbers =
    [
        ("Giulia Ferrari", "giulia"), ("Marco Bianchi", "marco"), ("Sara Conti", "sara"),
        ("Luca Rossi", "luca"), ("Chiara De Luca", "chiara"), ("Matteo Greco", "matteo"),
    ];

    /// <summary>A stable id for a demo profile. No Supabase account exists behind it, so nobody can sign in as them.</summary>
    private static Guid DemoUserId(string handle)
    {
        var bytes = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes($"bouldertime-demo:{handle}"));
        return new Guid(bytes);
    }

    private async Task<List<User>> SeedDemoClimbersAsync(CancellationToken ct)
    {
        var users = new List<User>();
        foreach (var (name, handle) in DemoClimbers)
        {
            var email = $"{handle}@{DemoEmailDomain}";
            var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);
            if (user is null)
            {
                // Deterministic id derived from the handle, so re-seeding finds the same profiles.
                user = User.Provision(DemoUserId(handle), email, name, null);
                user.SetLanguage("it");
                db.Users.Add(user);
            }
            users.Add(user);
        }
        await db.SaveChangesAsync(ct);
        return users;
    }

    /// <summary>
    /// Fills the demo gyms with the things a gym owner wants to see: sends spread over the past weeks, projects,
    /// ratings, community grade suggestions, comments, announcements and one report waiting in the moderation queue.
    /// </summary>
    private async Task SeedCommunityAsync(List<User> climbers, CancellationToken ct)
    {
        if (climbers.Count == 0 || await db.Comments.AnyAsync(c => true, ct)) return;
        var now = clock.UtcNow;
        var slugs = DemoGyms.Select(g => g.Slug).ToList();
        var gyms = await db.Gyms.Where(g => slugs.Contains(g.Slug)).ToListAsync(ct);
        var staffId = await db.GymStaff.Select(s => s.UserId).FirstOrDefaultAsync(ct);

        var comments = new[]
        {
            "Tallonaggio sulla presa grande e poi si chiude bene.",
            "Duro il primo movimento, il resto scorre.",
            "Bellissimo, tra i più belli della tracciatura.",
            "Attenzione al piede destro sul volume: scivola.",
            "Fatto al terzo tentativo, questione di sequenza.",
            "Secondo me è un filo più duro del grado.",
        };

        var rnd = new Random(20260920); // fixed seed: the demo looks the same on every machine
        foreach (var gym in gyms)
        {
            var boulders = await db.Boulders.Where(b => b.GymId == gym.Id && b.Status == BoulderStatus.Active)
                .OrderBy(b => b.Id).Take(10).ToListAsync(ct);
            if (boulders.Count == 0) continue;
            var sectors = await db.Sectors.Where(s => s.GymId == gym.Id).OrderBy(s => s.SortOrder).ToListAsync(ct);
            var systems = await db.GradeSystems.Where(s => s.GymId == gym.Id).OrderBy(s => s.SortOrder).ToListAsync(ct);
            var primary = systems.FirstOrDefault();
            var values = primary is null
                ? []
                : await db.GradeValues.Where(v => v.GradeSystemId == primary.Id && v.IsActive).OrderBy(v => v.Rank).ToListAsync(ct);

            for (var c = 0; c < climbers.Count; c++)
            {
                var climber = climbers[c];
                if (!await db.GymFollows.AnyAsync(f => f.UserId == climber.Id && f.GymId == gym.Id, ct))
                {
                    var follow = GymFollow.Create(climber.Id, gym.Id, now.AddDays(-60));
                    follow.Update(isFavorite: c % 3 == 0, notificationsEnabled: true);
                    db.GymFollows.Add(follow);
                }

                // One row per climber and boulder: the cycle below can land on the same boulder twice,
                // and the database (rightly) refuses a second attempt for the same pair.
                var touched = new HashSet<Guid>();

                // Sends spread over the last weeks, plus one open project each.
                var sends = 3 + (c % 4);
                for (var i = 0; i < sends && i < boulders.Count; i++)
                {
                    var boulder = boulders[(c + i * 2) % boulders.Count];
                    if (!touched.Add(boulder.Id)) continue;
                    if (await db.BoulderAttempts.AnyAsync(a => a.UserId == climber.Id && a.BoulderId == boulder.Id, ct)) continue;
                    var attempt = BoulderAttempt.Start(climber.Id, boulder.Id);
                    attempt.Record(1 + rnd.Next(4), completed: true, now.AddDays(-rnd.Next(1, 60)));
                    db.BoulderAttempts.Add(attempt);
                    if (i % 2 == 0) db.BoulderRatings.Add(BoulderRating.Create(climber.Id, boulder.Id, 3 + rnd.Next(3)));
                    if (primary is not null && values.Count > 0 && i == 0)
                        db.GradeSuggestions.Add(GradeSuggestion.Create(boulder.Id, climber.Id, primary.Id, values[Math.Min(values.Count - 1, 4 + rnd.Next(3))].Id));
                }

                var project = boulders[(c + 5) % boulders.Count];
                if (touched.Add(project.Id) && !await db.BoulderAttempts.AnyAsync(a => a.UserId == climber.Id && a.BoulderId == project.Id, ct))
                {
                    var attempt = BoulderAttempt.Start(climber.Id, project.Id);
                    attempt.Record(4 + rnd.Next(8), completed: false, now);
                    db.BoulderAttempts.Add(attempt);
                }

                db.Comments.Add(Comment.Create(boulders[c % boulders.Count].Id, climber.Id, comments[c % comments.Length]));
            }

            if (staffId == Guid.Empty) continue;
            var author = staffId;
            if (!await db.GymAnnouncements.AnyAsync(a => a.GymId == gym.Id, ct))
            {
                db.GymAnnouncements.Add(GymAnnouncement.Publish(gym.Id, author, AnnouncementType.Announcement,
                    "Ritracciata la sala principale",
                    "Venti blocchi nuovi da questo weekend, dal 4 al 7B. Passate a provarli!",
                    sectors.FirstOrDefault()?.Id, null, null, notifyFollowers: false));
                db.GymAnnouncements.Add(GymAnnouncement.Publish(gym.Id, author, AnnouncementType.Event,
                    "Serata boulder + pizza",
                    "Sessione libera fino a tardi e pizza insieme. Ingresso ridotto per i soci.",
                    null, null, now.AddDays(12), notifyFollowers: false));
            }
        }

        await db.SaveChangesAsync(ct);
        await SeedModerationQueueAsync(climbers, ct);
    }

    /// <summary>One open report, so the moderation queue isn't empty when a gym tries the staff area.</summary>
    private async Task SeedModerationQueueAsync(List<User> climbers, CancellationToken ct)
    {
        if (await db.Reports.AnyAsync(r => true, ct)) return;
        var comment = await db.Comments.OrderBy(c => c.CreatedAt).FirstOrDefaultAsync(ct);
        if (comment is null) return;
        var boulder = await db.Boulders.FirstAsync(b => b.Id == comment.BoulderId, ct);
        var reporter = climbers.FirstOrDefault(c => c.Id != comment.UserId);
        if (reporter is null) return;
        db.Reports.Add(Report.Create(reporter.Id, ReportEntityType.Comment, comment.Id, boulder.GymId,
            ReportReason.Misleading, "Secondo me il grado indicato nel commento confonde chi prova il blocco.", clock.UtcNow));
        await db.SaveChangesAsync(ct);
    }

    /// <summary>Demo gyms seeded before the Italian rewrite keep their sectors, renamed in place — history stays attached.</summary>
    private static readonly Dictionary<string, string> LegacySectorNames = new()
    {
        ["Main Hall"] = "Sala Grande", ["Cave"] = "Grotta", ["Slab"] = "Placca", ["Kids Wall"] = "Parete Bambini",
        ["Comp Wall"] = "Parete Gara", ["Overhang"] = "Strapiombo", ["Beginners"] = "Principianti", ["Room 2"] = "Sala 2",
        ["Front Room"] = "Sala Davanti", ["Training Boards"] = "Pan Güllich", ["Slab Corner"] = "Angolo Placca",
        ["Roof"] = "Tetto", ["Vertical"] = "Verticale",
    };

    private async Task RenameLegacySectorsAsync(Guid gymId, string[] expected, CancellationToken ct)
    {
        var sectors = await db.Sectors.Where(s => s.GymId == gymId).ToListAsync(ct);
        var renamed = false;
        foreach (var sector in sectors)
        {
            if (expected.Contains(sector.Name) || !LegacySectorNames.TryGetValue(sector.Name, out var italian)) continue;
            sector.Update(italian, sector.Description);
            renamed = true;
        }
        if (renamed) await db.SaveChangesAsync(ct);
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
    /// LOCAL DEVELOPMENT ONLY (the CLI refuses outside Development): makes every real account a platform admin and
    /// OWNER of every demo gym, so a freshly registered account can explore all areas immediately.
    /// Demo climbers are never promoted — they stay ordinary climbers — and any rights they got from an earlier
    /// version of this command are taken back. Personal climbing history is only invented when asked for.
    /// </summary>
    /// <returns>The emails of the accounts that were promoted.</returns>
    public async Task<IReadOnlyList<string>> PromoteAllForLocalDevelopmentAsync(bool withHistory = false, CancellationToken ct = default)
    {
        var demoSuffix = "@" + DemoEmailDomain;
        var users = await db.Users.Where(u => !u.Email.EndsWith(demoSuffix)).ToListAsync(ct);
        foreach (var user in users) user.GrantPlatformAdmin();

        // Undo what earlier versions did to demo climbers: they are climbers, not admins or gym staff.
        var demoUsers = await db.Users.Where(u => u.Email.EndsWith(demoSuffix)).ToListAsync(ct);
        foreach (var demo in demoUsers) demo.RevokePlatformAdmin();
        var demoIds = demoUsers.Select(u => u.Id).ToList();
        db.GymStaff.RemoveRange(await db.GymStaff.Where(m => demoIds.Contains(m.UserId)).ToListAsync(ct));
        await db.SaveChangesAsync(ct);

        foreach (var user in users)
        {
            await SeedAsync(user.Email, ct);
            if (withHistory) await SeedActivityAsync(user.Id, ct);
        }
        return users.Select(u => u.Email).ToList();
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
