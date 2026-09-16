using BoulderTime.Application.Abstractions;
using BoulderTime.Infrastructure.Persistence;
using BoulderTime.Infrastructure.Seeding;
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
        return services;
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
