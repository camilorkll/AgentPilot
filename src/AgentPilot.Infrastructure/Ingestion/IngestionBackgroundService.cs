using AgentPilot.Application.Abstractions;
using AgentPilot.Application.Ingestion;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentPilot.Infrastructure.Ingestion;

/// <summary>
/// Worker que consume la cola de ingesta y procesa cada documento en segundo
/// plano. Corre como singleton durante toda la vida de la app.
/// </summary>
public class IngestionBackgroundService(
    IIngestionQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<IngestionBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Worker de ingesta iniciado.");

        await foreach (var job in queue.DequeueAllAsync(stoppingToken))
        {
            try
            {
                // El DbContext y el repositorio son 'scoped': el worker (singleton)
                // debe abrir un scope nuevo por cada trabajo.
                using var scope = scopeFactory.CreateScope();
                var ingestion = scope.ServiceProvider.GetRequiredService<IDocumentIngestionService>();
                await ingestion.ProcessAsync(job, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error procesando el trabajo de ingesta {Id}.", job.DocumentId);
            }
        }
    }
}
