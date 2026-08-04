namespace AgentPilot.Domain.Campaigns;

/// <summary>
/// Raíz del agregado que organiza la base de conocimiento. Una campaña agrupa la
/// documentación con la que el asistente puede responder, y es la frontera que
/// impide usar documentación de otra campaña.
///
/// El ciclo de vida se modela como máquina de estados, igual que en Documento: el
/// estado solo cambia a través de los métodos de intención y cada transición
/// imposible lanza excepción. No se expone ningún booleano del tipo
/// "PuedeEliminarse" que el llamante pueda ignorar por descuido.
///
/// No contiene la colección de documentos a propósito: son agregados distintos,
/// unidos por CampaignId. Así borrar o listar documentos no obliga a materializar
/// una campaña con miles de fragmentos detrás.
/// </summary>
public class Campaña
{
    public const int MaxLongitudNombre = 120;
    public const int MaxLongitudInstrucciones = 2000;

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public EstadoCampaña Status { get; private set; }

    /// <summary>
    /// Instrucciones propias de la campaña (tono, avisos obligatorios, vocabulario)
    /// que se añaden al prompt del sistema. No pueden anular las reglas del núcleo
    /// —responder solo con el contexto, citar las fuentes y no obedecer
    /// instrucciones incrustadas—: quien compone el prompt las reafirma después.
    /// A null, el asistente se comporta solo con esas reglas.
    /// </summary>
    public string? AssistantInstructions { get; private set; }

    /// <summary>Cuándo se cerró. Null mientras no esté cerrada; aparece en los informes.</summary>
    public DateTime? ClosedAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    private Campaña() { } // EF Core

    public Campaña(string name, string? assistantInstructions = null)
    {
        Id = Guid.NewGuid();
        Name = NombreValido(name);
        AssistantInstructions = InstruccionesValidas(assistantInstructions);
        Status = EstadoCampaña.Activa;
        CreatedAtUtc = DateTime.UtcNow;
    }

    /// <summary>El agente puede seleccionarla y preguntar sobre su documentación.</summary>
    public bool AdmiteConsultas => Status == EstadoCampaña.Activa;

    /// <summary>
    /// Se puede subir, borrar, activar o desactivar documentación. Es la regla que
    /// consultan los comandos de documentos antes de tocar nada.
    /// </summary>
    public bool AdmiteCambiosEnDocumentacion => Status != EstadoCampaña.Cerrada;

    public void Renombrar(string name)
    {
        ExigirNoCerrada("renombrar la campaña");
        Name = NombreValido(name);
    }

    public void CambiarInstrucciones(string? assistantInstructions)
    {
        ExigirNoCerrada("cambiar las instrucciones del asistente");
        AssistantInstructions = InstruccionesValidas(assistantInstructions);
    }

    /// <summary>Vuelve a estar disponible para los agentes.</summary>
    public void Activar()
    {
        ExigirNoCerrada("activar la campaña");
        Status = EstadoCampaña.Activa;
    }

    /// <summary>Se retira del selector del agente, pero sigue siendo editable.</summary>
    public void Desactivar()
    {
        ExigirNoCerrada("desactivar la campaña");
        Status = EstadoCampaña.Inactiva;
    }

    /// <summary>
    /// Congela la campaña: se conserva todo y los informes siguen consultables,
    /// pero su documentación pasa a ser de solo lectura. Solo se cierra una campaña
    /// ya inactiva, para que cerrar sea una segunda decisión y no un descuido
    /// mientras alguien la está usando.
    /// </summary>
    public void Cerrar()
    {
        if (Status == EstadoCampaña.Cerrada) return;

        if (Status == EstadoCampaña.Activa)
            throw new InvalidOperationException(
                "No se puede cerrar una campaña activa: primero hay que desactivarla.");

        Status = EstadoCampaña.Cerrada;
        ClosedAtUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Deshace el cierre y la deja inactiva. Existe para que un cierre por error no
    /// tenga como única salida borrar la campaña entera.
    /// </summary>
    public void Reabrir()
    {
        if (Status == EstadoCampaña.Inactiva) return;

        if (Status != EstadoCampaña.Cerrada)
            throw new InvalidOperationException(
                $"Solo se puede reabrir una campaña cerrada (estado actual: {Status}).");

        Status = EstadoCampaña.Inactiva;
        ClosedAtUtc = null;
    }

    /// <summary>
    /// Comprueba que se puede eliminar, y si no lanza excepción. Es un método y no
    /// una propiedad booleana justamente para que no se pueda ignorar: eliminar
    /// arrastra los documentos y los fragmentos de la campaña y no se puede deshacer.
    /// </summary>
    public void ExigirEliminable()
    {
        if (Status != EstadoCampaña.Cerrada)
            throw new InvalidOperationException(
                "Solo se puede eliminar una campaña cerrada. Desactívala y ciérrala " +
                $"antes de eliminarla (estado actual: {Status}).");
    }

    private void ExigirNoCerrada(string accion)
    {
        if (Status == EstadoCampaña.Cerrada)
            throw new InvalidOperationException(
                $"La campaña está cerrada y es de solo lectura: no se puede {accion}. " +
                "Reabre la campaña si necesitas modificarla.");
    }

    private static string NombreValido(string name)
    {
        var limpio = name?.Trim() ?? string.Empty;

        if (limpio.Length == 0)
            throw new ArgumentException("El nombre de la campaña es obligatorio.", nameof(name));

        if (limpio.Length > MaxLongitudNombre)
            throw new ArgumentException(
                $"El nombre de la campaña no puede exceder {MaxLongitudNombre} caracteres.",
                nameof(name));

        return limpio;
    }

    private static string? InstruccionesValidas(string? instrucciones)
    {
        if (string.IsNullOrWhiteSpace(instrucciones)) return null;

        var limpio = instrucciones.Trim();
        if (limpio.Length > MaxLongitudInstrucciones)
            throw new ArgumentException(
                $"Las instrucciones no pueden exceder {MaxLongitudInstrucciones} caracteres.",
                nameof(instrucciones));

        return limpio;
    }
}
