using AgentPilot.Domain.Conversations;

namespace AgentPilot.Domain.Tests;

public class ConversationTests
{
    /// <summary>Campaña de la conversación: se fija al crearla y no cambia.</summary>
    private static readonly Guid Campaña = Guid.NewGuid();

    [Fact]
    public void UnaConversacionSinCampaña_NoTieneSentido()
    {
        // El historial se reenvía al modelo en cada turno; sin campaña no se sabría con
        // qué corpus se está respondiendo.
        Assert.Throws<ArgumentException>(() => new Conversation(Guid.Empty));
    }

    [Fact]
    public void AddUserMessage_DerivaElTituloDeLaPrimeraPregunta()
    {
        var conversation = new Conversation(Campaña);

        conversation.AddUserMessage("¿Puedo cambiar de tarifa?");

        Assert.Equal("¿Puedo cambiar de tarifa?", conversation.Title);
        Assert.Single(conversation.Messages);
    }

    [Fact]
    public void Titulo_SeTruncaSiLaPreguntaEsMuyLarga()
    {
        var conversation = new Conversation(Campaña);
        var preguntaLarga = new string('a', 200);

        conversation.AddUserMessage(preguntaLarga);

        Assert.Equal(81, conversation.Title!.Length); // 80 + '…'
        Assert.EndsWith("…", conversation.Title);
    }

    [Fact]
    public void AddAssistantMessage_GuardaContenidoYCitas()
    {
        var conversation = new Conversation(Campaña);
        conversation.AddUserMessage("¿Cuánto cuesta la tarifa Nova Mini?");
        var citas = new[]
        {
            new Citation(Guid.NewGuid(), "Catálogo tarifas", Guid.NewGuid(), "Nova Mini: 9,90 €/mes", 0.82),
        };

        var respuesta = conversation.AddAssistantMessage("Cuesta 9,90 €/mes.", citas);

        Assert.Equal(MessageRole.Assistant, respuesta.Role);
        Assert.Single(respuesta.Citations);
        Assert.Equal("Catálogo tarifas", respuesta.Citations.First().DocumentTitle);
        Assert.Equal(2, conversation.Messages.Count);
    }

    [Fact]
    public void MensajeDeUsuario_NoTieneCitas()
    {
        var conversation = new Conversation(Campaña);

        var mensaje = conversation.AddUserMessage("hola");

        Assert.Equal(MessageRole.User, mensaje.Role);
        Assert.Empty(mensaje.Citations);
    }
}
