using System.IdentityModel.Tokens.Jwt;
using AgentPilot.Domain.Users;
using AgentPilot.Infrastructure.Auth;
using AgentPilot.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace AgentPilot.Integration.Tests;

public class JwtTokenGeneratorTests
{
    [Fact]
    public void Generate_ProduceUnTokenConElRolYElUsuario()
    {
        var generator = new JwtTokenGenerator(Options.Create(new JwtOptions
        {
            Issuer = "AgentPilot",
            Audience = "AgentPilot",
            SigningKey = "clave-de-test-suficientemente-larga-1234567890",
            ExpiryMinutes = 60,
        }));
        var user = new User("admin", "hash-irrelevante", UserRole.Admin);

        var (token, expiresAt) = generator.Generate(user);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
        Assert.Equal("admin", jwt.Claims.First(c => c.Type == "role").Value);
        Assert.Equal("admin", jwt.Claims.First(c => c.Type == "sub").Value);
        Assert.Equal("AgentPilot", jwt.Issuer);
        Assert.True(expiresAt > DateTime.UtcNow.AddMinutes(50));
    }
}
