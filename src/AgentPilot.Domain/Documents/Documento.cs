namespace AgentPilot.Domain.Documents;

/// <summary>
/// Raíz del agregado de la base de conocimiento. Modela un documento y el
/// ciclo de vida de su ingesta como una máquina de estados: el estado solo
/// cambia a través de los métodos de intención (MarcarProcesando/Indexado/
/// Fallido), nunca asignando Status desde fuera. Así las reglas viven aquí y
/// se pueden testear sin base de datos ni IA.
/// </summary>
public class Documento
{
    private readonly List<Chunk> _chunks = [];

    public Guid Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string FileName { get; private set; } = string.Empty;
    public EstadoIngesta Status { get; private set; }

    /// <summary>Nº de chunks indexados. Null hasta que la ingesta termina.</summary>
    public int? ChunkCount { get; private set; }

    /// <summary>
    /// Modelo de embeddings con el que se indexó (p. ej. "text-embedding-3-small").
    /// Es clave: las consultas deben usar el MISMO modelo, porque distintos
    /// modelos producen vectores incompatibles (ver ADR-005).
    /// </summary>
    public string? EmbeddingModel { get; private set; }

    public string? ErrorMessage { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Solo lectura hacia fuera: los chunks se añaden vía MarcarIndexado.</summary>
    public IReadOnlyCollection<Chunk> Chunks => _chunks.AsReadOnly();

    private Documento() { } // EF Core

    public Documento(string title, string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("El nombre de fichero es obligatorio.", nameof(fileName));

        Id = Guid.NewGuid();
        FileName = fileName;
        Title = string.IsNullOrWhiteSpace(title) ? fileName : title;
        Status = EstadoIngesta.Pending;
        CreatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>El worker toma el documento y empieza a procesarlo.</summary>
    public void MarcarProcesando()
    {
        if (Status is not (EstadoIngesta.Pending or EstadoIngesta.Failed))
            throw new InvalidOperationException(
                $"No se puede procesar un documento en estado {Status}.");

        Status = EstadoIngesta.Processing;
        ErrorMessage = null;
    }

    /// <summary>Ingesta completada: se fijan los chunks y el modelo usado.</summary>
    public void MarcarIndexado(string embeddingModel, IEnumerable<Chunk> chunks)
    {
        if (Status != EstadoIngesta.Processing)
            throw new InvalidOperationException(
                $"Solo se puede indexar un documento en proceso (estado actual: {Status}).");

        _chunks.Clear();
        _chunks.AddRange(chunks);

        if (_chunks.Count == 0)
            throw new InvalidOperationException("La ingesta no produjo ningún chunk.");

        EmbeddingModel = embeddingModel;
        ChunkCount = _chunks.Count;
        Status = EstadoIngesta.Ready;
        ErrorMessage = null;
    }

    /// <summary>La ingesta falló; se registra el motivo para diagnóstico.</summary>
    public void MarcarFallido(string error)
    {
        Status = EstadoIngesta.Failed;
        ErrorMessage = error;
    }
}
