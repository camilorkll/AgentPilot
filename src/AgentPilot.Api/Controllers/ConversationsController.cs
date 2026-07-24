using AgentPilot.Api.Contracts;
using AgentPilot.Application.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace AgentPilot.Api.Controllers;

[ApiController]
[Route("api/v1/conversations")]
public class ConversationsController(IConversationRepository conversations) : ControllerBase
{
    /// <summary>Recupera el historial de una conversación con sus mensajes y citas.</summary>
    [HttpGet("{conversationId:guid}")]
    public async Task<ActionResult<ConversationResponse>> GetById(
        Guid conversationId, CancellationToken cancellationToken)
    {
        var conversation = await conversations.GetByIdAsync(conversationId, cancellationToken);
        return conversation is null ? NotFound() : conversation.ToResponse();
    }
}
