using AgentPilot.Api.Contracts;
using AgentPilot.Application.Abstractions;
using AgentPilot.Application.Feedback;
using AgentPilot.Domain.Conversations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgentPilot.Api.Controllers;

[ApiController]
[Route("api/v1/feedback")]
[Authorize] // agente o admin; el listado de revisión exige admin aparte
public class FeedbackController(
    IFeedbackService feedback, IFeedbackRepository repository) : ControllerBase
{
    /// <summary>Cuántas respuestas valoradas devuelve el listado si no se pide otra cosa.</summary>
    private const int LimitePorDefecto = 50;
    private const int LimiteMaximo = 200;
    [HttpPost]
    public async Task<IActionResult> Submit(
        [FromBody] FeedbackRequest request, CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<FeedbackRating>(request.Rating, ignoreCase: true, out var rating))
            return BadRequest(new { code = "validation_error", message = "rating debe ser 'positive' o 'negative'." });

        try
        {
            // User.Identity.Name = claim 'sub' (NameClaimType configurado en Program.cs).
            await feedback.SubmitAsync(
                request.MessageId, rating, request.Comment, User.Identity?.Name, cancellationToken);
            return StatusCode(StatusCodes.Status201Created);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { code = "not_found", message = ex.Message });
        }
    }

    /// <summary>
    /// Respuestas valoradas, más recientes primero, para revisarlas. Requiere rol
    /// `admin`: un agente valora, pero no revisa lo que valoraron los demás.
    ///
    /// Devuelve solo el intercambio valorado (pregunta + respuesta), no la conversación
    /// completa; para verla hay que pedirla explícitamente por su id. Es una decisión de
    /// privacidad, no una limitación técnica: revisar por qué falló una respuesta no
    /// exige leer todo lo que el cliente contó (ver SECURITY.md).
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<IReadOnlyList<RatedAnswerResponse>>> List(
        [FromQuery] string? rating,
        [FromQuery] Guid? campaignId,
        [FromQuery(Name = "operator")] string? @operator,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        FeedbackRating? parsed = null;
        if (!string.IsNullOrWhiteSpace(rating))
        {
            if (!Enum.TryParse<FeedbackRating>(rating, ignoreCase: true, out var value))
                return BadRequest(new
                {
                    code = "validation_error",
                    message = "rating debe ser 'positive' o 'negative' (o ausente para ambas).",
                });
            parsed = value;
        }

        var filter = new RatedAnswerFilter(
            parsed, campaignId, @operator, Math.Clamp(limit ?? LimitePorDefecto, 1, LimiteMaximo));

        var answers = await repository.ListRatedAnswersAsync(filter, cancellationToken);
        return answers.Select(a => a.ToResponse()).ToList();
    }
}
