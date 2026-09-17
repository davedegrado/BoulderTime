using System.Net.Http.Headers;
using BoulderTime.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BoulderTime.Tests.Infrastructure;

public sealed class ApiFactory(PostgresFixture postgres) : WebApplicationFactory<Program>
{
    /// <summary>Per-factory folder for the local object storage used in tests.</summary>
    public string StorageRoot { get; } = Path.Combine(Path.GetTempPath(), "bouldertime-tests", Guid.NewGuid().ToString("N"));

    /// <summary>Replace services for a specific test (e.g. a fake geocoder).</summary>
    public Action<IServiceCollection>? OverrideServices { get; init; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Database", postgres.ConnectionString);
        builder.UseSetting("Supabase:Url", TestTokens.SupabaseUrl);
        builder.UseSetting("Supabase:JwtSecret", TestTokens.Secret);
        builder.UseSetting("Storage:Provider", "Local");
        builder.UseSetting("Storage:Local:RootPath", StorageRoot);
        builder.UseSetting("Geocoding:Provider", "None"); // never call external services from tests
        builder.ConfigureTestServices(services => OverrideServices?.Invoke(services));
    }

    /// <summary>Creates the schema from the current model. Replaced by MigrateAsync once migrations are committed.</summary>
    public async Task ResetDatabaseAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.ExecuteSqlRawAsync($"DROP SCHEMA IF EXISTS {AppDbContext.Schema} CASCADE");
        if (db.Database.GetMigrations().Any())
            await db.Database.MigrateAsync();
        else
            await db.Database.EnsureCreatedAsync();
    }

    public HttpClient ClientFor(Guid userId, string email = "climber@example.com", object? metadata = null)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestTokens.For(userId, email, metadata));
        return client;
    }

    public async Task<T> WithDbAsync<T>(Func<AppDbContext, Task<T>> work)
    {
        await using var scope = Services.CreateAsyncScope();
        return await work(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }
}
