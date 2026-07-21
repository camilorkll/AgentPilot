using AgentPilot.Application.Abstractions;
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
    ILogger<DocumentIngestionService> logger) : IDocumentIngestionService
{
    public async Task<Documento> SubmitAsync(
        string fileName, string? title, Stream content, CancellationToken cancellationToken = default)
    {
        if (!extractor.Supports(fileName))
            throw new NotSupportedException(
                $"Formato de fichero no soportado: '{Path.GetExtension(fileName)}'.");

        // Copiamos los bytes para llevarlos en el trabajo: la petición HTTP
        // termina enseguida y el stream original se cerraría.
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);

        var document = new Documento(title ?? fileName, fileName);
        await repository.AddAsync(document, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);

        await queue.EnqueueAsync(
            new IngestionJob(document.Id, fileName, buffer.ToArray()), cancellationToken);

        logger.LogInformation("Documento {Id} encolado para ingesta ({File}).", document.Id, fileName);
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
