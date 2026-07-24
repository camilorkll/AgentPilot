using System.Text.Json;
using AgentPilot.Api.Contracts;
using AgentPilot.Application.Chat;
using Microsoft.AspNetCore.Mvc;

namespace AgentPilot.Api.Controllers;

[ApiController]
[Route("api/v1/chat")]
public class ChatController(IAskQuestionService ask) : ControllerBase
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Pregunta RAG con respuesta en streaming (Server-Sent Events).</summary>
    [HttpPost("ask")]
    public async Task Ask([FromBody] AskRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
        {
            Response.StatusCode = StatusCodes.Status400BadRequest;
            await Response.WriteAsJsonAsync(
                new { code = "validation_error", message = "La pregunta es obligatoria." },
                cancellationToken);
            return;
        }

        var sseStarted = false;
        try
        {
            await foreach (var evt in ask.AskAsync(request.Question, request.ConversationId, cancellationToken))
            {
                if (!sseStarted) { StartSse(); sseStarted = true; }
                var (name, payload) = MapEvent(evt);
                await WriteEventAsync(name, payload, cancellationToken);
            }
        }
        catch (KeyNotFoundException) when (!Response.HasStarted)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            await Response.WriteAsJsonAsync(
                new { code = "not_found", message = "Conversación no encontrada." }, cancellationToken);
        }
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
