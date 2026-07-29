namespace AgentPilot.Application.Abstractions;

/// <summary>
/// Usuario que realiza la petición en curso. La capa de aplicación lo necesita para
/// atribuir la telemetría a cada operador sin conocer HTTP ni el formato del token.
/// </summary>
public interface ICurrentUser
{
    /// <summary>Nombre del operador, o null si la acción no tiene usuario asociado.</summary>
    string? UserName { get; }
}
