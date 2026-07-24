using AgentPilot.Domain.Conversations;

namespace AgentPilot.Domain.Tests;

public class ConversationTests
{
    [Fact]
    public void AddUserMessage_DerivaElTituloDeLaPrimeraPregunta()
    {
        var conversation = new Conversation();

        conversation.AddUserMessage("¿Puedo cambiar de tarifa?");

        Assert.Equal("¿Puedo cambiar de tarifa?", conversation.Title);
        Assert.Single(conversation.Messages);
    }

    [Fact]
    public void Titulo_SeTruncaSiLaPreguntaEsMuyLarga()
    {
        var conversation = new Conversation();
        var preguntaLarga = new string('a', 200);

        conversation.AddUserMessage(preguntaLarga);

        Assert.Equal(81, conversation.Title!.Length); // 80 + '…'
        Assert.EndsWith("…", conversation.Title);
    }

    [Fact]
    public void AddAssistantMessage_GuardaContenidoYCitas()
    {
        var conversation = new Conversation();
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
        var conversation = new Conversation();

        var mensaje = conversation.AddUserMessage("hola");

        Assert.Equal(MessageRole.User, mensaje.Role);
        Assert.Empty(mensaje.Citations);
    }
}
