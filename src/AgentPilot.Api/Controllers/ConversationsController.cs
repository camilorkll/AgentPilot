using AgentPilot.Api.Contracts;
using AgentPilot.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgentPilot.Api.Controllers;

[ApiController]
[Route("api/v1/conversations")]
[Authorize] // agente o admin
public class ConversationsController(IConversationRepository conversations) : ControllerBase
{
    /// <summary>
    /// Recupera el historial de una conversación con sus mensajes, citas y la valoración
    /// de cada respuesta (si la tiene). La valoración viaja aquí para que al reabrir una
    /// conversación se vea lo ya votado, en vez de ofrecer votar de nuevo algo que ya
    /// se valoró.
    /// </summary>
    [HttpGet("{conversationId:guid}")]
    public async Task<ActionResult<ConversationResponse>> GetById(
        Guid conversationId, CancellationToken cancellationToken)
    {
        var conversation = await conversations.GetByIdAsync(conversationId, cancellationToken);
        if (conversation is null) return NotFound();

        var feedback = await conversations.ListFeedbackAsync(conversationId, cancellationToken);
        return conversation.ToResponse(feedback);
    }
}
