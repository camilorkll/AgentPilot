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
