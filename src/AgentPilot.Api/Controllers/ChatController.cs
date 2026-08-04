using System.Text.Json;
using AgentPilot.Api.Contracts;
using AgentPilot.Application.Campaigns;
using AgentPilot.Application.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgentPilot.Api.Controllers;

[ApiController]
[Route("api/v1/chat")]
[Authorize] // agente o admin
public class ChatController(IAskQuestionService ask) : ControllerBase
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Pregunta RAG con respuesta en streaming (Server-Sent Events).</summary>
    [HttpPost("ask")]
    public async Task Ask([FromBody] AskRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            await ErrorAsync(StatusCodes.Status400BadRequest,
                "validation_error", "La pregunta es obligatoria.", cancellationToken);
            return;
        }

        if (request.CampaignId is null || request.CampaignId == Guid.Empty)
        {
            await ErrorAsync(StatusCodes.Status400BadRequest, "validation_error",
                "Hay que indicar la campaña sobre la que se pregunta (campaignId).",
                cancellationToken);
            return;
        }

        var sseStarted = false;
        try
        {
            var events = ask.AskAsync(
                request.Question, request.CampaignId.Value, request.ConversationId, cancellationToken);

            await foreach (var evt in events)
            {
                if (!sseStarted) { StartSse(); sseStarted = true; }
                var (name, payload) = MapEvent(evt);
                await WriteEventAsync(name, payload, cancellationToken);
            }
        }
        // Los errores se traducen solo si el stream no ha empezado: una vez enviado el
        // primer evento SSE ya no se puede cambiar el código de estado.
        catch (KeyNotFoundException ex) when (!Response.HasStarted)
        {
            await ErrorAsync(StatusCodes.Status404NotFound, "not_found", ex.Message, cancellationToken);
        }
        catch (CampaignNotActiveException ex) when (!Response.HasStarted)
        {
            await ErrorAsync(StatusCodes.Status409Conflict,
                "campaign_not_active", ex.Message, cancellationToken);
        }
        catch (CampaignMismatchException ex) when (!Response.HasStarted)
        {
            await ErrorAsync(StatusCodes.Status409Conflict,
                "campaign_mismatch", ex.Message, cancellationToken);
        }
    }

    private async Task ErrorAsync(int status, string code, string message, CancellationToken ct)
    {
        Response.StatusCode = status;
        await Response.WriteAsJsonAsync(new { code, message }, ct);
    }

    private void StartSse()
    {
        Response.ContentType = "text/event-stream";
        Response.Headers.CacheControl = "no-cache";
        Response.Headers["X-Accel-Buffering"] = "no"; // no bufferizar tras proxies (nginx)
    }

    private async Task WriteEventAsync(string name, object payload, CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload, Json);
        await Response.WriteAsync($"event: {name}\ndata: {json}\n\n", ct);
        await Response.Body.FlushAsync(ct); // empuja el evento al cliente al instante
    }

    private static (string Name, object Payload) MapEvent(AskEvent evt) => evt switch
    {
        TokenEvent t => ("token", new { text = t.Text }),
        CitationsEvent c => ("citations", c.Citations.Select(x => x.ToDto()).ToList()),
        UsageEvent u => ("usage",
            new UsageDto(u.Model, u.PromptTokens, u.CompletionTokens, u.EstimatedCostUsd, u.LatencyMs)),
        DoneEvent d => ("done", new { conversationId = d.ConversationId }),
        _ => ("message", new { }),
    };
}
