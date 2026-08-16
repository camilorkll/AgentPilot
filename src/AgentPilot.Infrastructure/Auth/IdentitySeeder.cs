using AgentPilot.Application.Abstractions;
using AgentPilot.Domain.Users;

namespace AgentPilot.Infrastructure.Auth;

/// <summary>
/// Crea los usuarios de prueba al arrancar si la tabla está vacía. Las
/// contraseñas se guardan hasheadas; en claro solo aquí y documentadas en el
/// README (credenciales de demo).
/// </summary>
public static class IdentitySeeder
{
    public const string AdminUser = "admin";
    public const string AdminPassword = "admin1234";
    public const string AgentUser = "agente";
    public const string AgentPassword = "agente1234";

    /// <summary>
    /// Agentes adicionales. Existen para que haya **más de un operador** desde el primer
    /// arranque: sin ellos, el filtro por agente de la pantalla de revisión y el desglose
    /// por operador de las métricas no se pueden probar sin dar de alta usuarios a mano,
    /// que es justo lo que hubo que hacer en producción.
    /// </summary>
    public static readonly (string Usuario, string Contraseña)[] AgentesAdicionales =
    [
        ("laura", "laura1234"),
        ("marcos", "marcos1234"),
    ];

    public static async Task SeedAsync(
        IUserRepository users, IPasswordHasher hasher, CancellationToken cancellationToken = default)
    {
        // Solo siembra una base vacía: en un entorno ya en marcha los usuarios son datos
        // reales y no se tocan.
        if (await users.AnyAsync(cancellationToken))
            return;

        await users.AddAsync(new User(AdminUser, hasher.Hash(AdminPassword), UserRole.Admin), cancellationToken);
        await users.AddAsync(new User(AgentUser, hasher.Hash(AgentPassword), UserRole.Agent), cancellationToken);

        foreach (var (usuario, contraseña) in AgentesAdicionales)
            await users.AddAsync(new User(usuario, hasher.Hash(contraseña), UserRole.Agent), cancellationToken);

        await users.SaveChangesAsync(cancellationToken);
    }
}
