using AgentPilot.Domain.Documents;

namespace AgentPilot.Api.Contracts;

/// <summary>DTO de salida de un documento (coincide con el esquema Document del OpenAPI).</summary>
public record DocumentResponse(
    Guid Id,
    string Title,
    string FileName,
    string Status,
    int? ChunkCount,
    string? EmbeddingModel,
    string? ErrorMessage,
    DateTime CreatedAtUtc,
    bool IsActive);

/// <summary>Fragmento indexado de un documento, tal como lo usa la búsqueda.</summary>
public record DocumentChunkResponse(int Ordinal, string Content, int CharCount);

/// <summary>Contenido indexado de un documento, para consultarlo desde la interfaz.</summary>
public record DocumentContentResponse(
    Guid Id,
    string Title,
    string FileName,
    string? EmbeddingModel,
    IReadOnlyList<DocumentChunkResponse> Chunks);

/// <summary>Petición de borrado múltiple.</summary>
public record DeleteDocumentsRequest(IReadOnlyList<Guid> DocumentIds);

/// <summary>Activa o desactiva uno o varios documentos.</summary>
public record SetActiveRequest(IReadOnlyList<Guid> DocumentIds, bool IsActive);

/// <summary>Resultado del borrado múltiple.</summary>
public record DeleteDocumentsResponse(int Deleted, IReadOnlyList<Guid> NotFound);

public static class DocumentMappings
{
    public static DocumentResponse ToResponse(this Documento d) => new(
        d.Id,
        d.Title,
        d.FileName,
        d.Status.ToString().ToLowerInvariant(), // pending/processing/ready/failed
        d.ChunkCount,
        d.EmbeddingModel,
        d.ErrorMessage,
        d.CreatedAtUtc,
        d.IsActive);

    public static DocumentContentResponse ToContentResponse(this Documento d) => new(
        d.Id,
        d.Title,
        d.FileName,
        d.EmbeddingModel,
        d.Chunks
            .OrderBy(c => c.Ordinal)
            .Select(c => new DocumentChunkResponse(c.Ordinal, c.Content, c.Content.Length))
            .ToList());
}
