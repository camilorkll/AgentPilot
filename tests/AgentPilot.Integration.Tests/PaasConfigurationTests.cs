using AgentPilot.Api.Startup;

namespace AgentPilot.Integration.Tests;

/// <summary>
/// La cadena de conexión puede llegar en formato URI (así la exponen Railway, Render
/// o Heroku) o como cadena clave=valor de Npgsql. Estos tests fijan la traducción.
/// </summary>
public class PaasConfigurationTests
{
    [Theory]
    [InlineData("postgresql://user:secret@db.railway.internal:5432/railway")]
    [InlineData("postgres://user:secret@db.railway.internal:5432/railway")]
    public void UriDePostgres_SeTraduceACadenaNpgsql(string uri)
    {
        var result = PaasConfiguration.NormalizePostgresConnectionString(uri);

        Assert.Contains("Host=db.railway.internal", result);
        Assert.Contains("Port=5432", result);
        Assert.Contains("Database=railway", result);
        Assert.Contains("Username=user", result);
        Assert.Contains("Password=secret", result);
        // Prefer, no Require: hay redes internas de PaaS que no ofrecen TLS.
        Assert.Contains("SSL Mode=Prefer", result);
    }

    [Fact]
    public void UriSinPuerto_UsaElPuertoPorDefecto()
    {
        var result = PaasConfiguration.NormalizePostgresConnectionString(
            "postgresql://user:secret@host/base");

        Assert.Contains("Port=5432", result);
    }

    [Fact]
    public void ContrasenaConCaracteresEscapados_SeDecodifica()
    {
        var result = PaasConfiguration.NormalizePostgresConnectionString(
            "postgresql://user:p%40ss%3Aword@host:5432/base");

        Assert.Contains("Password=p@ss:word", result);
    }

    [Fact]
    public void CadenaNpgsqlExistente_SeDevuelveIntacta()
    {
        const string original = "Host=localhost;Port=5433;Database=agentpilot;Username=u;Password=p";

        Assert.Equal(original, PaasConfiguration.NormalizePostgresConnectionString(original));
    }
}
