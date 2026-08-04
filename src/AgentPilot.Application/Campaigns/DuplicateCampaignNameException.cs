namespace AgentPilot.Application.Campaigns;

/// <summary>
/// Ya existe una campaña con ese nombre (sin distinguir mayúsculas). La API la traduce
/// a 409. Evita que "Luz y Gas" y "luz y gas" convivan como dos campañas distintas, algo
/// que confundiría a cualquiera que las viera en el selector sin saber en cuál está la
/// documentación buena.
/// </summary>
public class DuplicateCampaignNameException(string name)
    : InvalidOperationException($"Ya existe una campaña llamada '{name}'.")
{
    public string Name { get; } = name;
}
