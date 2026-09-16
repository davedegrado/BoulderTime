using BoulderTime.Application.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace BoulderTime.Infrastructure.Persistence;

/// <summary>Used by <c>dotnet ef</c>. Reads the connection string from the environment only.</summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("ConnectionStrings__Database")
                 ?? "Host=127.0.0.1;Port=54322;Database=postgres;Username=postgres;Password=postgres";
        var options = new DbContextOptionsBuilder<AppDbContext>();
        DependencyInjection.ConfigureDbContext(options, cs);
        return new AppDbContext(options.Options, new SystemClock());
    }
}
