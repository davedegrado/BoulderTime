using BoulderTime.Infrastructure.Persistence;
using BoulderTime.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;

namespace BoulderTime.Api.Cli;

/// <summary>
/// Operator commands that must never be reachable over HTTP.
///   dotnet run --project src/BoulderTime.Api -- migrate
///   dotnet run --project src/BoulderTime.Api -- seed [--owner you@example.com]
///   dotnet run --project src/BoulderTime.Api -- grant-platform-admin you@example.com
///   dotnet run --project src/BoulderTime.Api -- revoke-platform-admin you@example.com
/// </summary>
public static class AdminCli
{
    public static bool IsCommand(string[] args) =>
        args.Length > 0 && args[0] is "migrate" or "seed" or "dev-promote-all" or "grant-platform-admin" or "revoke-platform-admin";

    public static async Task<int> RunAsync(WebApplication app, string[] args)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        switch (args[0])
        {
            case "migrate":
                await db.Database.MigrateAsync();
                Console.WriteLine("Database is up to date.");
                return 0;

            case "seed":
                var ownerIndex = Array.IndexOf(args, "--owner");
                var owner = ownerIndex > 0 && ownerIndex + 1 < args.Length ? args[ownerIndex + 1] : null;
                try
                {
                    Console.WriteLine(await scope.ServiceProvider.GetRequiredService<DemoSeeder>().SeedAsync(owner));
                    return 0;
                }
                catch (InvalidOperationException ex)
                {
                    Console.Error.WriteLine(ex.Message);
                    return 1;
                }

            case "dev-promote-all":
                if (!app.Environment.IsDevelopment())
                {
                    Console.Error.WriteLine("dev-promote-all is only available in the Development environment.");
                    return 1;
                }
                var promoted = await scope.ServiceProvider.GetRequiredService<DemoSeeder>().PromoteAllForLocalDevelopmentAsync();
                Console.WriteLine($"Promoted {promoted} user(s) to platform admin and demo-gym owner.");
                return 0;

            case "grant-platform-admin" or "revoke-platform-admin" when args.Length == 2:
                var email = args[1].Trim().ToLowerInvariant();
                var matches = await db.Users.Where(u => u.Email == email).ToListAsync();
                if (matches.Count == 0)
                {
                    Console.Error.WriteLine($"No user with email {email}. They must sign in to BoulderTime once first.");
                    return 1;
                }
                foreach (var user in matches)
                {
                    if (args[0] == "grant-platform-admin") user.GrantPlatformAdmin(); else user.RevokePlatformAdmin();
                }
                await db.SaveChangesAsync();
                Console.WriteLine($"{(args[0] == "grant-platform-admin" ? "Granted" : "Revoked")} platform admin for {email} ({matches.Count} account(s)).");
                return 0;

            default:
                Console.Error.WriteLine("Usage: migrate | seed [--owner <email>] | grant-platform-admin <email> | revoke-platform-admin <email>");
                return 2;
        }
    }
}
