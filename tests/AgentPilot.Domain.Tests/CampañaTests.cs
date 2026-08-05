using AgentPilot.Domain.Campaigns;

namespace AgentPilot.Domain.Tests;

/// <summary>
/// El ciclo de vida de una campaña se prueba sin base de datos: las reglas viven en
/// el dominio, así que aquí se comprueba lo que de verdad importa —qué transiciones
/// son imposibles y qué se puede hacer en cada estado— sin montar nada.
/// </summary>
public class CampañaTests
{
    [Fact]
    public void NuevaCampaña_NaceActivaYAdmiteTodo()
    {
        var campaña = new Campaña("Luz y Gas Premium");

        Assert.Equal(EstadoCampaña.Activa, campaña.Status);
        Assert.True(campaña.AdmiteConsultas);
        Assert.True(campaña.AdmiteCambiosEnDocumentacion);
        Assert.Null(campaña.ClosedAtUtc);
    }

    [Fact]
    public void Nombre_SeRecortaYNoPuedeEstarVacio()
    {
        Assert.Equal("Luz y Gas", new Campaña("  Luz y Gas  ").Name);

        Assert.Throws<ArgumentException>(() => new Campaña("   "));
        Assert.Throws<ArgumentException>(() => new Campaña(new string('x', 121)));
    }

    [Fact]
    public void Desactivar_LaRetiraDelSelectorPeroSigueEditable()
    {
        var campaña = new Campaña("TeleNova");

        campaña.Desactivar();

        // Es la diferencia con "cerrada": inactiva puede estar preparándose.
        Assert.False(campaña.AdmiteConsultas);
        Assert.True(campaña.AdmiteCambiosEnDocumentacion);
    }

    [Fact]
    public void Cerrar_ExigeQueEsteInactiva()
    {
        var campaña = new Campaña("TeleNova");

        // Cerrar una campaña en uso sería un descuido con consecuencias: hay que
        // desactivarla antes, para que cerrar sea una segunda decisión.
        var error = Assert.Throws<InvalidOperationException>(() => campaña.Cerrar());
        Assert.Contains("desactivarla", error.Message);
        Assert.Equal(EstadoCampaña.Activa, campaña.Status);

        campaña.Desactivar();
        campaña.Cerrar();

        Assert.Equal(EstadoCampaña.Cerrada, campaña.Status);
        Assert.NotNull(campaña.ClosedAtUtc);
    }

    [Fact]
    public void Cerrada_EsDeSoloLectura()
    {
        var campaña = Cerrada();

        Assert.False(campaña.AdmiteConsultas);
        Assert.False(campaña.AdmiteCambiosEnDocumentacion);

        var instrucciones = new AssistantPromptSettings("cercano", null, null, null, null);

        Assert.Throws<InvalidOperationException>(() => campaña.Renombrar("Otro nombre"));
        Assert.Throws<InvalidOperationException>(() => campaña.CambiarInstruccionesDelAsistente(instrucciones));
        Assert.Throws<InvalidOperationException>(() => campaña.Activar());
        Assert.Throws<InvalidOperationException>(() => campaña.Desactivar());
    }

    [Fact]
    public void Reabrir_DevuelveLaCampañaAInactiva()
    {
        var campaña = Cerrada();

        campaña.Reabrir();

        // Reabrir existe para que un cierre por error no obligue a borrarlo todo.
        Assert.Equal(EstadoCampaña.Inactiva, campaña.Status);
        Assert.Null(campaña.ClosedAtUtc);
        Assert.True(campaña.AdmiteCambiosEnDocumentacion);
    }

    [Fact]
    public void Reabrir_UnaCampañaActiva_NoTieneSentido()
    {
        var campaña = new Campaña("TeleNova");

        Assert.Throws<InvalidOperationException>(() => campaña.Reabrir());
    }

    [Fact]
    public void SoloSePuedeEliminarUnaCampañaCerrada()
    {
        var campaña = new Campaña("TeleNova");
        Assert.Throws<InvalidOperationException>(() => campaña.ExigirEliminable());

        campaña.Desactivar();
        Assert.Throws<InvalidOperationException>(() => campaña.ExigirEliminable());

        campaña.Cerrar();
        campaña.ExigirEliminable(); // no lanza
    }

    [Fact]
    public void NuevaCampaña_NaceSinInstruccionesPropias()
    {
        var campaña = new Campaña("TeleNova");

        Assert.True(campaña.AssistantPrompt.EstáVacío);
    }

    [Fact]
    public void CambiarInstruccionesDelAsistente_SustituyeElBloqueDeCampaña()
    {
        var campaña = new Campaña("TeleNova");
        var instrucciones = new AssistantPromptSettings(
            "cercano", "breve", "Recuerda verificar la identidad.", ["garantizado"], "Sé conciso.");

        campaña.CambiarInstruccionesDelAsistente(instrucciones);

        Assert.Same(instrucciones, campaña.AssistantPrompt);
        Assert.False(campaña.AssistantPrompt.EstáVacío);
    }

    private static Campaña Cerrada()
    {
        var campaña = new Campaña("TeleNova");
        campaña.Desactivar();
        campaña.Cerrar();
        return campaña;
    }
}
