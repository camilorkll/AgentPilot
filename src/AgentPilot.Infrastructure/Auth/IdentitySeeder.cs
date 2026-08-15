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

    public static async Task SeedAsync(
        IUserRepository users, IPasswordHasher hasher, CancellationToken cancellationToken = default)
    {
        if (await users.AnyAsync(cancellationToken))
            return;

        await users.AddAsync(new User(AdminUser, hasher.Hash(AdminPassword), UserRole.Admin), cancellationToken);
        await users.AddAsync(new User(AgentUser, hasher.Hash(AgentPassword), UserRole.Agent), cancellationToken);
        await users.SaveChangesAsync(cancellationToken);
    }
}
