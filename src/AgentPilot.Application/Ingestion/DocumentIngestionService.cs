using AgentPilot.Application.Abstractions;
using AgentPilot.Application.Campaigns;
using AgentPilot.Domain.Documents;
using Microsoft.Extensions.Logging;

namespace AgentPilot.Application.Ingestion;

/// <summary>
/// Orquesta la ingesta de documentos. No sabe de PDFs, de OpenAI ni de pgvector:
/// depende solo de puertos (extractor, chunker, embeddings, repositorio, cola).
/// </summary>
public class DocumentIngestionService(
    IDocumentRepository repository,
    IDocumentTextExtractor extractor,
    ITextChunker chunker,
    IEmbeddingService embeddings,
    IIngestionQueue queue,
    CampaignGuard campaigns,
    ILogger<DocumentIngestionService> logger) : IDocumentIngestionService
{
    public async Task<Documento> SubmitAsync(
        Guid campaignId, string fileName, string? title, Stream content,
        bool replaceExisting = false, CancellationToken cancellationToken = default)
    {
        if (!extractor.Supports(fileName))
            throw new NotSupportedException(
                $"Formato de fichero no soportado: '{Path.GetExtension(fileName)}'.");

        // La campaña debe existir y admitir cambios antes de tocar nada: si no, se
        // aceptaría el fichero y el fallo aparecería después, en el worker.
        var campaign = await campaigns.ExigirEditableAsync(campaignId, cancellationToken);

        // Un mismo fichero ingerido dos veces duplicaría sus fragmentos en el índice
        // vectorial: o se avisa al cliente, o se sustituye el documento anterior. El
        // duplicado se busca solo dentro de la campaña: el mismo nombre en otra es
        // legítimo, son corpus independientes.
        var existing = await repository.GetByFileNameAsync(campaignId, fileName, cancellationToken);
        if (existing is not null)
        {
            if (!replaceExisting)
                throw new DuplicateDocumentException(existing.Id, fileName);

            repository.Delete(existing); // los fragmentos se borran en cascada
            await repository.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Documento {Id} ({File}) reemplazado por una nueva versión.", existing.Id, fileName);
        }

        // Copiamos los bytes para llevarlos en el trabajo: la petición HTTP
        // termina enseguida y el stream original se cerraría.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);

        var document = new Documento(campaign.Id, title ?? fileName, fileName);
        await repository.AddAsync(document, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        await queue.EnqueueAsync(
            new IngestionJob(document.Id, fileName, buffer.ToArray()), cancellationToken);

        logger.LogInformation(
            "Documento {Id} encolado para ingesta ({File}) en la campaña {Campaign}.",
            document.Id, fileName, campaign.Name);
        return document;
    }

    public async Task ProcessAsync(IngestionJob job, CancellationToken cancellationToken = default)
    {
        var document = await repository.GetByIdAsync(job.DocumentId, cancellationToken);
        if (document is null)
        {
            logger.LogWarning("El documento {Id} ya no existe; se descarta el trabajo.", job.DocumentId);
            return;
        }

        try
        {
            document.MarcarProcesando();
            await repository.SaveChangesAsync(cancellationToken);

            // 1) Extraer texto plano del fichero.
            using var stream = new MemoryStream(job.Content);
            var text = await extractor.ExtractTextAsync(stream, job.FileName, cancellationToken);

            // 2) Trocear en fragmentos con solapamiento.
            var fragments = chunker.Split(text);
            if (fragments.Count == 0)
                throw new InvalidOperationException("El documento no produjo texto indexable.");

            // 3) Generar los embeddings de todos los fragmentos (una sola petición).
            var vectors = await embeddings.EmbedBatchAsync(fragments, cancellationToken);

            // 4) Construir los chunks e indexar el documento.
            var chunks = fragments
                .Select((fragment, i) => new Chunk(i, fragment, vectors[i]))
                .ToList();

            document.MarcarIndexado(embeddings.ModelName, chunks);
            await repository.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Documento {Id} indexado: {Count} chunks con el modelo {Model}.",
                document.Id, chunks.Count, embeddings.ModelName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Fallo al ingerir el documento {Id}.", job.DocumentId);
            document.MarcarFallido(ex.Message);
            await repository.SaveChangesAsync(cancellationToken);
        }
    }
}
