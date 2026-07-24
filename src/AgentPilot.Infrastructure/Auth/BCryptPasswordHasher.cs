using AgentPilot.Application.Abstractions;

namespace AgentPilot.Infrastructure.Auth;

/// <summary>Hashing de contraseñas con BCrypt (sal automática y factor de coste).</summary>
public class BCryptPasswordHasher : IPasswordHasher
{
    public string Hash(string password) => BCrypt.Net.BCrypt.HashPassword(password);

    public bool Verify(string password, string passwordHash) =>
        BCrypt.Net.BCrypt.Verify(password, passwordHash);
}
