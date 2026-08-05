namespace AgentPilot.Domain.Campaigns;

/// <summary>
/// Instrucciones propias de una campaña para el asistente: tono, nivel de detalle, un
/// aviso obligatorio, vocabulario a evitar y un bloque de texto libre acotado. No son
/// reglas del sistema, son instrucciones de negocio, y nunca pueden anular el núcleo
/// del prompt (grounding, citas, anti-inyección): ese núcleo vive en código
/// (<see cref="AgentPilot.Application.Chat.SystemPromptBuilder"/>) y se reafirma
/// después de este bloque al componer el prompt real.
///
/// Se ofrecen campos concretos y no un textarea en blanco: sirve mejor a un
/// supervisor que no es ingeniero de prompts, y acota la superficie de texto libre a
/// un único campo con límite, en vez de dejar que cualquier frase pueda intentar
/// colarse como si fuera una regla del sistema.
///
/// Vacía por completo (todos los campos sin informar) significa "hereda el
/// comportamiento por defecto": el asistente responde solo con el núcleo, exactamente
/// como antes de que existiera esta clase.
/// </summary>
public class AssistantPromptSettings
{
    public const int MaxLongitudAviso = 300;
    public const int MaxLongitudInstruccionesLibres = 2000;
    public const int MaxPalabrasEvitar = 20;
    public const int MaxLongitudPalabraEvitar = 60;

    public static readonly string[] TonosValidos = ["cercano", "neutro", "formal"];
    public static readonly string[] NivelesDetalleValidos = ["breve", "normal", "detallado"];

    /// <summary>
    /// Sin ningún campo informado: equivalente a no tener instrucciones propias. Es un
    /// valor de conveniencia para comparar o construir a partir de él (tests, "restaurar
    /// a los valores por defecto"); NUNCA se debe asignar este mismo objeto como
    /// AssistantPrompt de más de una Campaña rastreada por el mismo DbContext. EF Core
    /// identifica una entidad owned por referencia (no por valor): dos Campaña distintas
    /// apuntando al mismo objeto compartido rompen el change tracker con "AssistantPromptSettings.CampañaId
    /// is part of a key and so cannot be modified" en cuanto se añade la segunda. Por eso
    /// Campaña.AssistantPrompt se inicializa con una instancia propia (ver más abajo), no
    /// con este singleton.
    /// </summary>
    public static readonly AssistantPromptSettings Vacío = new(null, null, null, null, null);

    public string? Tone { get; private set; }
    public string? DetailLevel { get; private set; }
    public string? MandatoryNotice { get; private set; }

    // List<string> y no IReadOnlyList<string>: EF Core solo sabe mapear una colección
    // de primitivos dentro de una columna jsonb (ver CampañaConfiguration) si el tipo
    // es concreto y no de solo lectura. El setter privado sigue impidiendo reemplazar
    // la lista desde fuera; solo el constructor la puebla, ya validada.
    //
    // El "get" se autorrepara a propósito: al materializar desde una columna jsonb sin
    // la clave "AvoidWords" (comprobado en vivo con la fila de compatibilidad de
    // TeleNova, cuyo valor por defecto de la migración es '{}'), EF escribe directamente
    // sobre el campo de respaldo sin pasar por el constructor ni por este setter, y deja
    // null. Sin este resguardo, cualquier .Count o .Contains sobre AvoidWords lanzaría
    // NullReferenceException para esas filas.
    private List<string>? _avoidWords = [];
    public List<string> AvoidWords
    {
        get => _avoidWords ??= [];
        private set => _avoidWords = value;
    }

    public string? ExtraInstructions { get; private set; }

    private AssistantPromptSettings() { } // EF Core (mapeado como jsonb, ver CampañaConfiguration)

    public AssistantPromptSettings(
        string? tone, string? detailLevel, string? mandatoryNotice,
        IEnumerable<string>? avoidWords, string? extraInstructions)
    {
        Tone = ValidarTono(tone);
        DetailLevel = ValidarNivel(detailLevel);
        MandatoryNotice = Acotar(mandatoryNotice, MaxLongitudAviso, nameof(mandatoryNotice));
        AvoidWords = ValidarPalabras(avoidWords);
        ExtraInstructions = Acotar(extraInstructions, MaxLongitudInstruccionesLibres, nameof(extraInstructions));
    }

    public bool EstáVacío =>
        Tone is null && DetailLevel is null && MandatoryNotice is null &&
        AvoidWords.Count == 0 && ExtraInstructions is null;

    /// <summary>
    /// Patrones que, si aparecen en el texto libre o en el aviso, sugieren un intento de
    /// anular el núcleo en vez de una instrucción de negocio legítima. Es una
    /// advertencia, no un bloqueo: el núcleo se reafirma después de todos modos, así
    /// que el peor caso es una instrucción de campaña que no hace nada, nunca una que
    /// rompa el grounding o las citas. Avisa para que quien publica lo sepa, no para
    /// impedirlo.
    /// </summary>
    private static readonly string[] PatronesSospechosos =
    [
        "ignora", "olvida las reglas", "no cites", "sin citar", "responde siempre",
        "sin importar", "no reveles que", "cambia de rol", "actúa como", "eres ahora",
    ];

    public IReadOnlyList<string> AdviertePatronesSospechosos()
    {
        var texto = $"{MandatoryNotice} {ExtraInstructions}".ToLowerInvariant();
        return PatronesSospechosos.Where(p => texto.Contains(p)).ToList();
    }

    private static string? ValidarTono(string? tone)
    {
        if (string.IsNullOrWhiteSpace(tone)) return null;
        var limpio = tone.Trim().ToLowerInvariant();
        if (!TonosValidos.Contains(limpio))
            throw new ArgumentException(
                $"Tono no válido: '{tone}'. Usa uno de: {string.Join(", ", TonosValidos)}.", nameof(tone));
        return limpio;
    }

    private static string? ValidarNivel(string? level)
    {
        if (string.IsNullOrWhiteSpace(level)) return null;
        var limpio = level.Trim().ToLowerInvariant();
        if (!NivelesDetalleValidos.Contains(limpio))
            throw new ArgumentException(
                $"Nivel de detalle no válido: '{level}'. Usa uno de: {string.Join(", ", NivelesDetalleValidos)}.",
                nameof(level));
        return limpio;
    }

    private static List<string> ValidarPalabras(IEnumerable<string>? words)
    {
        var limpias = (words ?? [])
            .Select(w => w.Trim())
            .Where(w => w.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (limpias.Count > MaxPalabrasEvitar)
            throw new ArgumentException(
                $"No se pueden indicar más de {MaxPalabrasEvitar} palabras a evitar.", nameof(words));
        if (limpias.Any(w => w.Length > MaxLongitudPalabraEvitar))
            throw new ArgumentException(
                $"Cada palabra a evitar debe tener como máximo {MaxLongitudPalabraEvitar} caracteres.", nameof(words));

        return limpias;
    }

    private static string? Acotar(string? text, int max, string paramName)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;
        var limpio = text.Trim();
        if (limpio.Length > max)
            throw new ArgumentException($"No puede exceder {max} caracteres.", paramName);
        return limpio;
    }
}
