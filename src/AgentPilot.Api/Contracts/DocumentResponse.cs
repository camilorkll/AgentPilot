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
    DateTime CreatedAtUtc);

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
        d.CreatedAtUtc);
}
