using AgentPilot.Application.Abstractions;
using AgentPilot.Application.Auth;
using AgentPilot.Domain.Users;

namespace AgentPilot.Application.Tests;

public class AuthServiceTests
{
    private static AuthService Build(params User[] users) =>
        new(new FakeUsers(users), new FakeHasher(), new FakeTokens());

    [Fact]
    public async Task Login_ConCredencialesCorrectas_DevuelveToken()
    {
        var user = new User("admin", FakeHasher.HashOf("admin1234"), UserRole.Admin);
        var service = Build(user);

        var result = await service.LoginAsync("admin", "admin1234");

        Assert.NotNull(result);
        Assert.Equal("admin", result!.Role);
        Assert.Equal("token:admin", result.AccessToken);
    }

    [Fact]
    public async Task Login_ConContraseñaIncorrecta_DevuelveNull()
    {
        var user = new User("admin", FakeHasher.HashOf("admin1234"), UserRole.Admin);
        var service = Build(user);

        Assert.Null(await service.LoginAsync("admin", "incorrecta"));
    }

    [Fact]
    public async Task Login_ConUsuarioInexistente_DevuelveNull()
    {
        var service = Build(); // sin usuarios

        Assert.Null(await service.LoginAsync("fantasma", "loquesea"));
    }

    [Fact]
    public async Task CadaLogin_DesplazaLaSesionAnterior()
    {
        // Un operador es una persona en un puesto: entrar por segunda vez —desde otro
        // navegador u otro equipo— deja sin valor al token de la primera.
        var user = new User("agente", FakeHasher.HashOf("agente1234"), UserRole.Agent);
        var service = Build(user);

        await service.LoginAsync("agente", "agente1234");
        var primera = user.SessionId;

        await service.LoginAsync("agente", "agente1234");
        var segunda = user.SessionId;

        Assert.NotNull(primera);
        Assert.NotEqual(primera, segunda);
        Assert.False(user.SesionVigente(primera));
        Assert.True(user.SesionVigente(segunda));
    }

    [Fact]
    public async Task UnLoginFallido_NoTocaLaSesionAbierta()
    {
        // Si una contraseña equivocada cerrara la sesión, cualquiera podría echar a un
        // agente de su puesto sin saber sus credenciales, solo con teclear su usuario.
        var user = new User("agente", FakeHasher.HashOf("agente1234"), UserRole.Agent);
        var service = Build(user);

        await service.LoginAsync("agente", "agente1234");
        var abierta = user.SessionId;

        Assert.Null(await service.LoginAsync("agente", "incorrecta"));

        Assert.Equal(abierta, user.SessionId);
        Assert.True(user.SesionVigente(abierta));
    }

    // --- Dobles ---
    private sealed class FakeUsers(User[] users) : IUserRepository
    {
        public Task<User?> GetByUsernameAsync(string u, CancellationToken ct = default)
            => Task.FromResult(users.FirstOrDefault(x => x.Username == u));
        public Task<bool> AnyAsync(CancellationToken ct = default) => Task.FromResult(users.Length > 0);
        public Task AddAsync(User user, CancellationToken ct = default) => Task.CompletedTask;
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeHasher : IPasswordHasher
    {
        public static string HashOf(string pwd) => "H:" + pwd;
        public string Hash(string password) => HashOf(password);
        public bool Verify(string password, string passwordHash) => HashOf(password) == passwordHash;
    }

    private sealed class FakeTokens : IJwtTokenGenerator
    {
        public (string AccessToken, DateTime ExpiresAtUtc) Generate(User user)
            => ($"token:{user.Username}", DateTime.UtcNow.AddHours(8));
    }
}
