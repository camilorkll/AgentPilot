using AgentPilot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace AgentPilot.Integration.Tests;

/// <summary>
/// Levanta un PostgreSQL+pgvector real y efímero (Testcontainers) para los tests
/// de integración, y le aplica las migraciones. Se destruye al terminar.
/// </summary>
public class PgVectorFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder()
        .WithImage("pgvector/pgvector:pg16")
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public AgentPilotDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AgentPilotDbContext>()
            .UseNpgsql(ConnectionString, npgsql => npgsql.UseVector())
            .Options;
        return new AgentPilotDbContext(options);
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
