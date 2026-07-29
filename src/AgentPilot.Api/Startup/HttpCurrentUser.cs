using AgentPilot.Application.Abstractions;

namespace AgentPilot.Api.Startup;

/// <summary>
/// Resuelve el operador de la petición a partir del claim 'sub' del JWT.
/// </summary>
public class HttpCurrentUser(IHttpContextAccessor accessor) : ICurrentUser
{
    public string? UserName => accessor.HttpContext?.User.Identity?.Name;
}
