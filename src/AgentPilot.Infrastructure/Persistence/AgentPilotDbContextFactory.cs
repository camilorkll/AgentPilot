using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace AgentPilot.Infrastructure.Persistence;

/// <summary>
/// Factoría usada SOLO en tiempo de diseño por 'dotnet ef' (generar/aplicar
/// migraciones). Permite crear el DbContext sin arrancar toda la aplicación,
/// evitando depender de la configuración completa del host (claves de OpenAI,
/// etc.). En ejecución normal se usa el DbContext registrado en DI.
/// </summary>
public class AgentPilotDbContextFactory : IDesignTimeDbContextFactory<AgentPilotDbContext>
{
    public AgentPilotDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? "Host=localhost;Port=5433;Database=agentpilot;Username=agentpilot;Password=agentpilot_dev";

        var options = new DbContextOptionsBuilder<AgentPilotDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.UseVector())
            .Options;

        return new AgentPilotDbContext(options);
    }
}
