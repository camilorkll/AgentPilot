namespace AgentPilot.Infrastructure.Configuration;

/// <summary>Sección "Embeddings": elige el proveedor y configura Ollama.</summary>
public class EmbeddingsOptions
{
    public const string SectionName = "Embeddings";

    /// <summary>"openai" (nube, por defecto) u "ollama" (local).</summary>
    public string Provider { get; set; } = "openai";

    /// <summary>URL base del servidor Ollama (solo si Provider = ollama).</summary>
    public string OllamaBaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>Modelo de embeddings local (768 dimensiones).</summary>
    public string OllamaModel { get; set; } = "nomic-embed-text";
}
