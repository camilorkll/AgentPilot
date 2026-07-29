using AgentPilot.Application.Abstractions;
using AgentPilot.Infrastructure.Auth;
using AgentPilot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentPilot.Api.Startup;

/// <summary>
/// Aplica las migraciones y siembra los usuarios de prueba en segundo plano.
/// Se hace tras el arranque (no bloqueándolo) para que el servidor acepte peticiones
/// y responda al healthcheck del hosting desde el primer momento: si la base de datos
/// tarda en estar disponible o algo falla, la aplicación sigue viva y el problema
/// queda registrado en el log en lugar de provocar un ciclo de reinicios.
/// </summary>
public class DatabaseInitializer(
    IServiceScopeFactory scopeFactory,
    ILogger<DatabaseInitializer> logger) : BackgroundService
{
    private const int MaxAttempts = 12;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var services = scope.ServiceProvider;

                await services.GetRequiredService<AgentPilotDbContext>()
                    .Database.MigrateAsync(stoppingToken);

                await IdentitySeeder.SeedAsync(
                    services.GetRequiredService<IUserRepository>(),
                    services.GetRequiredService<IPasswordHasher>(),
                    stoppingToken);

                logger.LogInformation("Base de datos lista: migraciones aplicadas y usuarios sembrados.");
                return;
            }
            catch (Exception ex) when (attempt < MaxAttempts && !stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning(ex,
                    "La base de datos no está lista (intento {Attempt}/{Max}); reintento en {Delay}s…",
                    attempt, MaxAttempts, RetryDelay.TotalSeconds);
                await Task.Delay(RetryDelay, stoppingToken);
            }
            catch (Exception ex)
            {
                // La aplicación sigue en pie: el healthcheck responde y el error queda
                // registrado para diagnóstico (p. ej. falta CREATE EXTENSION vector).
                logger.LogError(ex,
                    "No se pudo preparar la base de datos tras {Max} intentos. " +
                    "Revisa la cadena de conexión y que la extensión pgvector esté creada.",
                    MaxAttempts);
                return;
            }
        }
    }
}
