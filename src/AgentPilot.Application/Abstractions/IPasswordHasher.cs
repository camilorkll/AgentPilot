namespace AgentPilot.Application.Abstractions;

/// <summary>Hashing y verificación de contraseñas. Implementado con BCrypt en Infrastructure.</summary>
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string passwordHash);
}
