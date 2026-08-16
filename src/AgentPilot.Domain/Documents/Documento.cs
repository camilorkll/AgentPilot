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

    /// <summary>
    /// Campaña a la que pertenece. Se fija al crear el documento y no cambia: mover
    /// un documento de campaña alteraría en silencio lo que el asistente puede
    /// responder en dos campañas a la vez.
    /// </summary>
    public Guid CampaignId { get; private set; }

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

    /// <summary>
    /// Texto plano extraído del fichero, tal como se troceó. Se guarda para poder
    /// reindexar sin el fichero original (ADR-012): los fragmentos ya están cortados y
    /// solapados, así que no sirven para volver a trocear con otro criterio.
    ///
    /// No participa en la búsqueda —no se vectoriza ni se indexa—, solo existe como
    /// fuente para regenerar los fragmentos.
    ///
    /// Null en los documentos ingeridos antes de esta decisión: no hay de dónde
    /// recuperarlo, y esos documentos no se pueden reindexar hasta volver a subirlos.
    /// </summary>
    public string? ExtractedText { get; private set; }

    public string? ErrorMessage { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Se puede reindexar sin el fichero original.</summary>
    public bool PuedeReindexarse => !string.IsNullOrWhiteSpace(ExtractedText);

    /// <summary>
    /// Si está inactivo, sus fragmentos quedan fuera de la búsqueda: el asistente no
    /// puede recuperarlos ni citarlos. Sirve para retirar temporalmente información con
    /// vigencia (promociones caducadas, por ejemplo) sin perder lo ya indexado, de modo
    /// que reactivarlo sea inmediato y no haya que volver a vectorizar el documento.
    /// </summary>
    public bool IsActive { get; private set; } = true;

    /// <summary>Solo lectura hacia fuera: los chunks se añaden vía MarcarIndexado.</summary>
    public IReadOnlyCollection<Chunk> Chunks => _chunks.AsReadOnly();

    private Documento() { } // EF Core

    public Documento(Guid campaignId, string title, string fileName)
    {
        if (campaignId == Guid.Empty)
            throw new ArgumentException("El documento debe pertenecer a una campaña.", nameof(campaignId));
        if (string.IsNullOrWhiteSpace(fileName))
            throw new ArgumentException("El nombre de fichero es obligatorio.", nameof(fileName));

        Id = Guid.NewGuid();
        CampaignId = campaignId;
        FileName = fileName;
        Title = string.IsNullOrWhiteSpace(title) ? fileName : title;
        Status = EstadoIngesta.Pending;
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>Devuelve el documento a la base de conocimiento consultable.</summary>
    public void Activar() => IsActive = true;

    /// <summary>Retira el documento de las búsquedas sin perder lo indexado.</summary>
    public void Desactivar() => IsActive = false;

    /// <summary>El worker toma el documento y empieza a procesarlo.</summary>
    public void MarcarProcesando()
    {
        if (Status is not (EstadoIngesta.Pending or EstadoIngesta.Failed))
            throw new InvalidOperationException(
                $"No se puede procesar un documento en estado {Status}.");

        Status = EstadoIngesta.Processing;
        ErrorMessage = null;
    }

    /// <summary>
    /// Vuelve a poner en proceso un documento ya indexado para regenerar sus fragmentos
    /// desde el texto guardado. Es la única transición que sale de Ready, y existe
    /// aparte de <see cref="MarcarProcesando"/> para que reindexar sea una intención
    /// explícita y no un efecto colateral de reprocesar.
    /// </summary>
    public void MarcarReindexando()
    {
        if (!PuedeReindexarse)
            throw new InvalidOperationException(
                "No se puede reindexar este documento porque no se guardó su texto extraído " +
                "(se ingirió antes de que se persistiera). Hay que volver a subir el fichero.");

        if (Status is not (EstadoIngesta.Ready or EstadoIngesta.Failed))
            throw new InvalidOperationException(
                $"Solo se reindexa un documento ya indexado o fallido (estado actual: {Status}).");

        Status = EstadoIngesta.Processing;
        ErrorMessage = null;
    }

    /// <summary>
    /// Ingesta completada: se fijan los chunks, el modelo usado y el texto del que
    /// salieron.
    /// </summary>
    /// <param name="extractedText">
    /// Texto plano que se troceó. Es obligatorio y no tiene valor por defecto a
    /// propósito: si se pudiera omitir, un documento acabaría indexado sin la única
    /// fuente que permite reindexarlo después, y el descuido no daría error hasta meses
    /// más tarde, al intentar reindexar.
    /// </param>
    public void MarcarIndexado(string embeddingModel, IEnumerable<Chunk> chunks, string extractedText)
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
        ExtractedText = extractedText;
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
