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

    /// <summary>
    /// Sesión abierta ahora mismo. Se renueva en cada login y viaja dentro del token, de
    /// modo que solo el último emitido sigue valiendo: un operador es una persona en un
    /// puesto, y dos sesiones a la vez significan o credenciales compartidas o una sesión
    /// olvidada en otro sitio.
    ///
    /// Null mientras nadie haya entrado, y también en los usuarios anteriores a que esto
    /// existiera: sus tokens en circulación siguen valiendo hasta caducar, porque
    /// invalidarlos de golpe echaría de la aplicación a quien estuviera trabajando en
    /// mitad de una llamada.
    /// </summary>
    public Guid? SessionId { get; private set; }

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

    /// <summary>
    /// Abre una sesión nueva y desplaza a la anterior, si la había. Devuelve el
    /// identificador que viajará en el token.
    ///
    /// Gana la última y no la primera a propósito: rechazar el login nuevo dejaría fuera
    /// hasta ocho horas —lo que dura el token— a quien cerrara el navegador sin salir o
    /// cambiara de puesto, que es un caso corriente en un contact center y no tiene
    /// arreglo desde el lado del operador.
    /// </summary>
    public Guid AbrirSesion()
    {
        SessionId = Guid.NewGuid();
        return SessionId.Value;
    }

    /// <summary>
    /// La sesión del token sigue siendo la vigente.
    ///
    /// Un usuario sin <see cref="SessionId"/> acepta cualquier token válido: son los que
    /// no han vuelto a entrar desde que esto existe, y su token ya caduca solo.
    /// </summary>
    public bool SesionVigente(Guid? sesionDelToken)
        => SessionId is null || SessionId == sesionDelToken;
}
