namespace AgentPilot.Application.Abstractions;

/// <summary>
/// Puerto de generación de embeddings. La capa Application depende de esta
/// abstracción, no de un proveedor concreto: en Infrastructure hay una
/// implementación con OpenAI y otra con Ollama, y se elige por configuración.
/// </summary>
public interface IEmbeddingService
{
    /// <summary>
    /// Nombre del modelo activo (p. ej. "text-embedding-3-small"). Se guarda en
    /// Documento.EmbeddingModel: las consultas deben usar el mismo modelo con el
    /// que se indexó, porque distintos modelos producen vectores incompatibles.
    /// </summary>
    string ModelName { get; }

    /// <summary>Genera el vector de embedding de un único texto.</summary>
    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default);

    /// <summary>
    /// Genera los vectores de varios textos. Se ofrece en lote porque los
    /// proveedores permiten enviar muchos textos en una sola petición, lo que
    /// reduce latencia y coste frente a llamar uno a uno.
    /// </summary>
    Task<IReadOnlyList<float[]>> EmbedBatchAsync(
        IReadOnlyList<string> texts, CancellationToken cancellationToken = default);
}
