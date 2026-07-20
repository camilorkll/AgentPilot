namespace AgentPilot.Domain.Documents;

/// <summary>
/// Ciclo de vida de la ingesta de un documento. Coincide con el enum
/// IngestStatus del contrato OpenAPI (pending/processing/ready/failed).
/// </summary>
public enum EstadoIngesta
{
    /// <summary>Subido pero aún no procesado. Estado inicial.</summary>
    Pending,

    /// <summary>El worker está parseando/troceando/generando embeddings.</summary>
    Processing,

    /// <summary>Indexado correctamente: consultable por búsqueda vectorial.</summary>
    Ready,

    /// <summary>La ingesta falló; el motivo está en Documento.ErrorMessage.</summary>
    Failed
}
