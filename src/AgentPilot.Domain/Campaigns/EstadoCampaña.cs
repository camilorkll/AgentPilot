namespace AgentPilot.Domain.Campaigns;

/// <summary>
/// Ciclo de vida de una campaña. Coincide con el enum CampaignStatus del contrato
/// OpenAPI, que lo expone con nombre (inactive/active/closed) aunque aquí se
/// persista como entero.
/// </summary>
public enum EstadoCampaña
{
    /// <summary>
    /// Retirada del selector del agente: no responde consultas. Su documentación
    /// sigue siendo editable, porque una campaña inactiva puede estar preparándose.
    /// </summary>
    Inactiva = 0,

    /// <summary>Visible para los agentes y respondiendo consultas.</summary>
    Activa = 1,

    /// <summary>
    /// Solo lectura. Conserva documentos, fragmentos e informes, pero no admite
    /// cambios en su documentación. Es el único estado desde el que se puede
    /// eliminar: cerrar es la decisión consciente que precede al borrado.
    /// </summary>
    Cerrada = 2
}
