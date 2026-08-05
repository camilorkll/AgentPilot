namespace AgentPilot.Infrastructure.Configuration;

/// <summary>
/// Sección "Chat": elige el proveedor de generación de respuestas. Mismo patrón que
/// EmbeddingsOptions.Provider (Fase 2), aquí aplicado al chat para poder medir Ollama
/// en local frente a OpenAI (ver evals/COMPARATIVA-MODELOS.md). Ollama no va a
/// producción: es una comparativa, no una función del producto.
/// </summary>
public class ChatOptions
{
    public const string SectionName = "Chat";

    /// <summary>"openai" (nube, por defecto) u "ollama" (local).</summary>
    public string Provider { get; set; } = "openai";

    /// <summary>URL base del servidor Ollama (solo si Provider = ollama).</summary>
    public string OllamaBaseUrl { get; set; } = "http://localhost:11434";

    /// <summary>Modelo de chat local. 3B y no 8B: en esta máquina la inferencia es en CPU
    /// (sin GPU dedicada), y un 3B es unas tres veces más rápido con calidad suficiente
    /// para lo que se quiere observar en la comparativa.</summary>
    public string OllamaModel { get; set; } = "llama3.2:3b";

    /// <summary>
    /// Ventana de contexto en tokens, fijada explícitamente. Muchas instalaciones de
    /// Ollama usan 2048 por defecto; el prompt de AgentPilot (núcleo + bloque de
    /// campaña + 5 fragmentos de ~1000 caracteres + historial) lo desborda con
    /// facilidad, y el síntoma es desconcertante: el modelo descarta en silencio la
    /// parte que sobra y se abstiene o responde mal aunque las fuentes aparezcan bien
    /// en pantalla. 4096 deja margen sin exigir más RAM de la que ya sobra.
    /// </summary>
    public int OllamaNumCtx { get; set; } = 4096;
}
