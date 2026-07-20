using AgentPilot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentPilot.Infrastructure;

/// <summary>
/// Punto de entrada de la capa Infrastructure: registra sus servicios en el
/// contenedor de DI. La API llama a AddInfrastructure(...) y no necesita saber
/// qué hay dentro (DbContext, proveedores de IA, etc.).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Falta la cadena de conexión 'ConnectionStrings:Default'.");

        services.AddDbContext<AgentPilotDbContext>(options =>
            // UseVector() activa el mapeo del tipo pgvector en Npgsql.
            options.UseNpgsql(connectionString, npgsql => npgsql.UseVector()));

        return services;
    }
}
