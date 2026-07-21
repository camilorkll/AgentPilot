namespace AgentPilot.Application.Abstractions;

/// <summary>
/// Extrae texto plano de un fichero subido. El caso de uso de ingesta depende
/// de esta abstracción y no sabe si el origen es PDF, Markdown u otro formato.
/// </summary>
public interface IDocumentTextExtractor
{
    /// <summary>¿Sé extraer texto de este fichero? (según su extensión).</summary>
    bool Supports(string fileName);

    /// <summary>Devuelve el texto plano del contenido.</summary>
    Task<string> ExtractTextAsync(
        Stream content, string fileName, CancellationToken cancellationToken = default);
}
