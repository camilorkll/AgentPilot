using AgentPilot.Domain.Users;

namespace AgentPilot.Domain.Tests;

public class UserTests
{
    private static User Agente() => new("agente", "hash", UserRole.Agent);

    [Fact]
    public void UnUsuarioNuevo_NoTieneSesionAbierta()
    {
        Assert.Null(Agente().SessionId);
    }

    [Fact]
    public void SinSesionRegistrada_SeAceptaCualquierToken()
    {
        // Son los usuarios anteriores a que existiera la sesión única: sus tokens en
        // circulación siguen valiendo hasta caducar, en vez de echar de golpe a quien
        // estuviera atendiendo una llamada cuando se desplegó el cambio.
        var user = Agente();

        Assert.True(user.SesionVigente(Guid.NewGuid()));
        Assert.True(user.SesionVigente(null));
    }

    [Fact]
    public void AbrirSesion_InvalidaLaAnterior()
    {
        var user = Agente();
        var primera = user.AbrirSesion();
        var segunda = user.AbrirSesion();

        Assert.NotEqual(primera, segunda);
        Assert.False(user.SesionVigente(primera));
        Assert.True(user.SesionVigente(segunda));
    }

    [Fact]
    public void UnTokenSinSesion_NoValeSiElUsuarioYaTieneUna()
    {
        // Token emitido antes del cambio, usuario que ya ha vuelto a entrar: el token
        // viejo no puede seguir sirviendo, o la sesión única no serviría de nada.
        var user = Agente();
        user.AbrirSesion();

        Assert.False(user.SesionVigente(null));
    }
}
