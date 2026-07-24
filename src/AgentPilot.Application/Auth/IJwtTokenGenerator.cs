using AgentPilot.Domain.Users;

namespace AgentPilot.Application.Auth;

/// <summary>Genera un JWT firmado para un usuario autenticado.</summary>
public interface IJwtTokenGenerator
{
    (string AccessToken, DateTime ExpiresAtUtc) Generate(User user);
}
