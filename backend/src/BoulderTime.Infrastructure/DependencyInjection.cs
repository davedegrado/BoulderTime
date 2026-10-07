using BoulderTime.Application.Abstractions;
using BoulderTime.Infrastructure.Push;
using Microsoft.Extensions.Options;
using BoulderTime.Infrastructure.Persistence;
using BoulderTime.Infrastructure.Seeding;
using BoulderTime.Infrastructure.Storage;
using BoulderTime.Infrastructure.Geocoding;
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
        if ((configuration["Geocoding:Provider"] ?? "Nominatim").Equals("None", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IGeocoder, DisabledGeocoder>();
        else
            services.AddHttpClient<IGeocoder, NominatimGeocoder>(c =>
            {
                c.Timeout = TimeSpan.FromSeconds(10);
                c.DefaultRequestHeaders.UserAgent.ParseAdd("BoulderTime/1.0 (+https://github.com/davedegrado/BoulderTime)");
            });
        // Web push: the sender talks to the browsers' push services, the dispatcher does it outside the request.
        services.Configure<WebPushOptions>(configuration.GetSection("Push"));
        services.AddHttpClient<WebPushSender>(c => c.Timeout = TimeSpan.FromSeconds(10));
        services.AddHttpClient<FcmPushSender>(c => c.Timeout = TimeSpan.FromSeconds(10));
        services.AddSingleton<IPushSender>(sp => sp.GetRequiredService<WebPushSender>());
        services.AddSingleton<IPushSender>(sp => sp.GetRequiredService<FcmPushSender>());
        services.AddSingleton<IPushConfig>(sp => new PushConfig(sp.GetRequiredService<IOptions<WebPushOptions>>().Value.PublicKey));
        services.AddSingleton<PushDispatcher>();
        services.AddSingleton<IPushQueue>(sp => sp.GetRequiredService<PushDispatcher>());
        services.AddHostedService(sp => sp.GetRequiredService<PushDispatcher>());

        // Deleting an account: removing the sign-in, and the daily sweep that erases accounts past their week.
        services.AddHttpClient<IAuthAdmin, Supabase.SupabaseAuthAdmin>(c => c.Timeout = TimeSpan.FromSeconds(10));
        services.AddHostedService<Users.AccountErasureWorker>();

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


/// <summary>Exposes the public VAPID key to the application layer without handing it the whole options object.</summary>
internal sealed record PushConfig(string? PublicKey) : IPushConfig;
