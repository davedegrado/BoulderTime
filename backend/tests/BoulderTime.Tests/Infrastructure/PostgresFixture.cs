using Testcontainers.PostgreSql;

namespace BoulderTime.Tests.Infrastructure;

/// <summary>
/// Real PostgreSQL in Docker. Business rules in BoulderTime lean on unique constraints and FKs,
/// which the EF in-memory provider does not enforce — so tests run against the real engine.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();
    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

[CollectionDefinition(Name)]
public sealed class DatabaseCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "database";
}
