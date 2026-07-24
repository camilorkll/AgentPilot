using AgentPilot.Api.Contracts;
using AgentPilot.Application.Feedback;
using AgentPilot.Domain.Conversations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgentPilot.Api.Controllers;

[ApiController]
[Route("api/v1/feedback")]
[Authorize] // agente o admin
public class FeedbackController(IFeedbackService feedback) : ControllerBase
{
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
}
