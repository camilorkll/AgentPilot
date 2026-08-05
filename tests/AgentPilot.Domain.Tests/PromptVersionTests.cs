using AgentPilot.Domain.Campaigns;

namespace AgentPilot.Domain.Tests;

/// <summary>
/// PromptVersion es una fotografía serializada: lo que importa es que ToSettings()
/// reconstruya exactamente lo que se guardó, incluida la campaña vacía (restaurar por
/// defecto también es una versión).
/// </summary>
public class PromptVersionTests
{
    [Fact]
    public void ToSettings_ReconstruyeLoMismoQueSeGuardó()
    {
        var original = new AssistantPromptSettings(
            "cercano", "breve", "Verifica la identidad.", ["gratis", "garantizado"], "Sé conciso.");

        var version = new PromptVersion(Guid.NewGuid(), original, "ana");
        var reconstruido = version.ToSettings();

        Assert.Equal(original.Tone, reconstruido.Tone);
        Assert.Equal(original.DetailLevel, reconstruido.DetailLevel);
        Assert.Equal(original.MandatoryNotice, reconstruido.MandatoryNotice);
        Assert.Equal(original.AvoidWords, reconstruido.AvoidWords);
        Assert.Equal(original.ExtraInstructions, reconstruido.ExtraInstructions);
    }

    [Fact]
    public void ToSettings_ConCampañaVacía_ReconstruyeVacía()
    {
        var version = new PromptVersion(Guid.NewGuid(), AssistantPromptSettings.Vacío, "ana");

        Assert.True(version.ToSettings().EstáVacío);
    }

    [Fact]
    public void PublishedBy_EnBlancoSeGuardaComoDesconocido()
    {
        var version = new PromptVersion(Guid.NewGuid(), AssistantPromptSettings.Vacío, "   ");

        Assert.Equal("desconocido", version.PublishedBy);
    }

    [Fact]
    public void CadaVersiónTieneSuPropioIdYMarcaDeTiempo()
    {
        var a = new PromptVersion(Guid.NewGuid(), AssistantPromptSettings.Vacío, "ana");
        var b = new PromptVersion(Guid.NewGuid(), AssistantPromptSettings.Vacío, "ana");

        Assert.NotEqual(a.Id, b.Id);
        Assert.Equal(DateTimeKind.Utc, a.CreatedAtUtc.Kind);
    }
}
