using BoulderTime.Application.Abstractions;
using BoulderTime.Infrastructure.Persistence;
using BoulderTime.Infrastructure.Seeding;
using BoulderTime.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BoulderTime.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Database");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("ConnectionStrings:Database is not configured. See backend/.env.example.");

        services.AddSingleton<IClock, SystemClock>();
        services.AddDbContext<AppDbContext>(o => ConfigureDbContext(o, connectionString));
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());
        services.AddScoped<DemoSeeder>();
        AddStorage(services, configuration);
        return services;
    }

    private static void AddStorage(IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<StorageOptions>(configuration.GetSection(StorageOptions.Section));
        var provider = configuration[$"{StorageOptions.Section}:Provider"] ?? "Supabase";
        if (provider.Equals("Local", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<LocalObjectStorage>();
            services.AddSingleton<IObjectStorage>(sp => sp.GetRequiredService<LocalObjectStorage>());
            return;
        }
        if (!provider.Equals("Supabase", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Unknown Storage:Provider '{provider}'. Use 'Supabase' or 'Local'.");

        var serviceKey = configuration["Supabase:ServiceRoleKey"];
        if (string.IsNullOrWhiteSpace(serviceKey))
            throw new InvalidOperationException("Supabase:ServiceRoleKey is required when Storage:Provider is 'Supabase'. See backend/.env.example.");
        services.AddHttpClient<IObjectStorage, SupabaseObjectStorage>(c =>
        {
            c.Timeout = TimeSpan.FromSeconds(30);
            c.DefaultRequestHeaders.Add("apikey", serviceKey);
            c.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", serviceKey);
        });
    }

    internal static void ConfigureDbContext(DbContextOptionsBuilder options, string connectionString) =>
        options
            .UseNpgsql(connectionString, npgsql =>
            {
                npgsql.MigrationsHistoryTable("__ef_migrations_history", AppDbContext.Schema);
                npgsql.MigrationsAssembly(typeof(AppDbContext).Assembly.FullName);
            })
            .UseSnakeCaseNamingConvention();
}
