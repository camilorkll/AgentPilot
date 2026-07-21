using AgentPilot.Domain.Documents;

namespace AgentPilot.Application.Ingestion;

public interface IDocumentIngestionService
{
    /// <summary>Fase síncrona: crea el documento (Pending), lo guarda y encola su ingesta.</summary>
    Task<Documento> SubmitAsync(
        string fileName, string? title, Stream content, CancellationToken cancellationToken = default);

    /// <summary>Fase de fondo: extrae, trocea, vectoriza e indexa el documento.</summary>
    Task ProcessAsync(IngestionJob job, CancellationToken cancellationToken = default);
}
