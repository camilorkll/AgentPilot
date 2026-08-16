using AgentPilot.Application.Abstractions;

namespace AgentPilot.Application.Auth;

/// <summary>Resultado de un login correcto.</summary>
public sealed record LoginResult(string AccessToken, string Role, DateTime ExpiresAtUtc);

public interface IAuthService
{
    /// <summary>Valida credenciales y devuelve un token, o null si son incorrectas.</summary>
    Task<LoginResult?> LoginAsync(string username, string password, CancellationToken cancellationToken = default);
}

public class AuthService(
    IUserRepository users, IPasswordHasher passwordHasher, IJwtTokenGenerator tokenGenerator) : IAuthService
{
    public async Task<LoginResult?> LoginAsync(
        string username, string password, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByUsernameAsync(username, cancellationToken);

        // Verificamos el hash aunque el usuario no exista no aporta aquí, pero
        // devolvemos el mismo resultado (null) en ambos casos para no revelar
        // si el usuario existe.
        if (user is null || !passwordHasher.Verify(password, user.PasswordHash))
            return null;
        // CRK. Tenemos usuario y contraseña correctos, generamos el token.

        // Abrir sesión ANTES de firmar: el token lleva dentro la sesión que acaba de
        // quedar registrada, y con eso el anterior deja de valer. Se guarda primero para
        // que no pueda entregarse un token cuya sesión no llegó a persistirse.
        user.AbrirSesion();
        await users.SaveChangesAsync(cancellationToken);

        var (token, expiresAt) = tokenGenerator.Generate(user);

        return new LoginResult(token, user.RoleName, expiresAt);
    }
}
