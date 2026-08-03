namespace AgentPilot.Infrastructure.Configuration;

/// <summary>Sección "OpenAI" de la configuración (appsettings o variables OpenAI__*).</summary>
public class OpenAiOptions
{
    public const string SectionName = "OpenAI";

    /// <summary>Clave de API. Nunca en el repo: llega por variable de entorno / .env.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Modelo de embeddings (1536 dimensiones).</summary>
    public string EmbeddingModel { get; set; } = "text-embedding-3-small";

    /// <summary>
    /// Modelo de chat. Por defecto gpt-4o-mini: a igualdad de calidad medida sobre el set
    /// dorado, responde en menos de un segundo frente a los ~4 s de gpt-5-mini, que
    /// razona antes de emitir el primer token (ver evals/COMPARATIVA-MODELOS.md).
    /// </summary>
    public string ChatModel { get; set; } = "gpt-4o-mini";
}
