namespace AgentPilot.Domain.Users;

/// <summary>
/// Usuario del sistema. Guarda solo el HASH de la contraseña, nunca la
/// contraseña en claro (el hashing se hace en la capa de Infraestructura).
/// </summary>
public class User
{
    public Guid Id { get; private set; }
    public string Username { get; private set; } = string.Empty;
    public string PasswordHash { get; private set; } = string.Empty;
    public UserRole Role { get; private set; }

    private User() { } // EF

    public User(string username, string passwordHash, UserRole role)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException("El usuario es obligatorio.", nameof(username));
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException("El hash de contraseña es obligatorio.", nameof(passwordHash));

        Id = Guid.NewGuid();
        Username = username;
        PasswordHash = passwordHash;
        Role = role;
    }

    /// <summary>Nombre del rol en minúsculas ("agent"/"admin"), para el claim del JWT.</summary>
    public string RoleName => Role.ToString().ToLowerInvariant();
}
