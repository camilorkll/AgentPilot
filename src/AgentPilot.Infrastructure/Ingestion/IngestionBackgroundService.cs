using AgentPilot.Application.Abstractions;
using AgentPilot.Application.Ingestion;
using AgentPilot.Domain.Documents;
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

        await RescatarInterrumpidosAsync(stoppingToken);

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

    /// <summary>
    /// Saca del limbo los documentos que quedaron en <c>Processing</c> al pararse la
    /// aplicación.
    ///
    /// La cola vive en memoria, así que un reinicio —y en Railway cada despliegue lo
    /// es— pierde los trabajos pendientes y los que estaban en curso. El documento se
    /// quedaba marcado como "procesando" para siempre: ni indexado ni fallido, invisible
    /// para las búsquedas y sin que nada volviera a intentarlo.
    ///
    /// No se reintenta automáticamente: el fichero ya no está (los bytes viajaban en el
    /// trabajo perdido) y, si el fallo fuera del propio documento, reintentar en cada
    /// arranque sería un bucle. Se deja en un estado honesto y visible para que el
    /// administrador decida. Los que conserven fragmentos de una versión anterior
    /// vuelven a estar consultables, porque su contenido sigue siendo válido.
    /// </summary>
    private async Task RescatarInterrumpidosAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IDocumentRepository>();

            var interrumpidos = await repository.ListAsync(
                status: EstadoIngesta.Processing, cancellationToken: cancellationToken);

            if (interrumpidos.Count == 0) return;

            foreach (var documento in interrumpidos)
                documento.MarcarFallido(
                    "La ingesta se interrumpió al reiniciarse la aplicación. " +
                    "Vuelve a subir el fichero para completarla.");

            await repository.SaveChangesAsync(cancellationToken);

            logger.LogWarning(
                "Rescatados {Count} documentos que quedaron a medio procesar en el arranque anterior.",
                interrumpidos.Count);
        }
        catch (Exception ex)
        {
            // Que el rescate falle no debe impedir que el worker arranque: sin él, la
            // ingesta de documentos nuevos dejaría de funcionar por completo.
            logger.LogError(ex, "No se pudieron rescatar los documentos interrumpidos.");
        }
    }
}
