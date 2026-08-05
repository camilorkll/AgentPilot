using AgentPilot.Api.Contracts;
using AgentPilot.Application.Abstractions;
using AgentPilot.Application.Campaigns;
using AgentPilot.Application.Chat;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgentPilot.Api.Controllers;

[ApiController]
[Route("api/v1/campaigns")]
[Authorize] // /active es de cualquier usuario autenticado; el resto exige admin
public class CampaignsController(
    ICampaignService campaigns, IPromptPreviewService promptPreview, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "admin")]
    public async Task<IReadOnlyList<CampaignResponse>> List(CancellationToken cancellationToken)
        => (await campaigns.ListAsync(cancellationToken)).Select(c => c.ToResponse()).ToList();

    /// <summary>
    /// Campañas activas, para el selector del agente. Proyección reducida: no incluye
    /// las instrucciones del asistente, que no son de su incumbencia.
    /// </summary>
    [HttpGet("active")]
    public async Task<IReadOnlyList<CampaignSummaryResponse>> ListActive(CancellationToken cancellationToken)
        => (await campaigns.ListActiveAsync(cancellationToken)).Select(c => c.ToSummary()).ToList();

    [HttpGet("{campaignId:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<CampaignResponse>> GetById(
        Guid campaignId, CancellationToken cancellationToken)
    {
        try
        {
            return (await campaigns.GetAsync(campaignId, cancellationToken)).ToResponse();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { code = "campaign_not_found", message = ex.Message });
        }
    }

    [HttpPost]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Create(
        [FromBody] CampaignRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { code = "validation_error", message = "El nombre es obligatorio." });

        try
        {
            var created = await campaigns.CreateAsync(request.Name, cancellationToken);
            return CreatedAtAction(
                nameof(GetById), new { campaignId = created.Campaign.Id }, created.ToResponse());
        }
        catch (DuplicateCampaignNameException ex)
        {
            return Conflict(new { code = "duplicate_campaign_name", message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    [HttpPut("{campaignId:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<CampaignResponse>> Update(
        Guid campaignId, [FromBody] CampaignRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { code = "validation_error", message = "El nombre es obligatorio." });

        try
        {
            var updated = await campaigns.UpdateAsync(campaignId, request.Name, cancellationToken);
            return updated.ToResponse();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { code = "campaign_not_found", message = ex.Message });
        }
        catch (DuplicateCampaignNameException ex)
        {
            return Conflict(new { code = "duplicate_campaign_name", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // Cubre "está cerrada": ExigirNoCerrada() del dominio lanza este tipo.
            return Conflict(new { code = "campaign_closed", message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    /// <summary>
    /// Transición de estado, en su propio endpoint y no como un campo más del PUT:
    /// cerrar una campaña la congela, y eso no debe poder ocurrir como efecto
    /// secundario de renombrarla.
    /// </summary>
    [HttpPost("{campaignId:guid}/status")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<CampaignResponse>> SetStatus(
        Guid campaignId, [FromBody] CampaignStatusRequest request, CancellationToken cancellationToken)
    {
        Domain.Campaigns.EstadoCampaña status;
        try
        {
            status = CampaignMappings.ParseStatus(request.Status);
        }
        catch (FormatException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }

        try
        {
            var updated = await campaigns.SetStatusAsync(campaignId, status, cancellationToken);
            return updated.ToResponse();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { code = "campaign_not_found", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            // Cubre las transiciones que la propia campaña rechaza (cerrar una activa,
            // reabrir una que no está cerrada, etc.): el mensaje ya explica el porqué.
            return Conflict(new { code = "invalid_transition", message = ex.Message });
        }
    }

    /// <summary>
    /// Elimina la campaña y, en cascada, su corpus. Solo permitido si está cerrada: es
    /// irreversible y se lleva por delante todo lo indexado.
    /// </summary>
    [HttpDelete("{campaignId:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Delete(Guid campaignId, CancellationToken cancellationToken)
    {
        try
        {
            await campaigns.DeleteAsync(campaignId, cancellationToken);
            return NoContent();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { code = "campaign_not_found", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { code = "campaign_not_closed", message = ex.Message });
        }
    }

    /// <summary>Instrucciones vigentes de la campaña para el asistente.</summary>
    [HttpGet("{campaignId:guid}/prompt")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<AssistantPromptResponse>> GetPrompt(
        Guid campaignId, CancellationToken cancellationToken)
    {
        try
        {
            return (await campaigns.GetPromptAsync(campaignId, cancellationToken)).ToResponse();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { code = "campaign_not_found", message = ex.Message });
        }
    }

    /// <summary>
    /// Publica unas instrucciones nuevas: guardar el formulario vacío equivale a
    /// restaurar el comportamiento por defecto (solo el núcleo).
    /// </summary>
    [HttpPut("{campaignId:guid}/prompt")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<PromptUpdateResponse>> UpdatePrompt(
        Guid campaignId, [FromBody] AssistantPromptRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var settings = request.ToDomain();
            var result = await campaigns.UpdatePromptAsync(
                campaignId, settings, currentUser.UserName ?? "desconocido", cancellationToken);
            return result.ToResponse();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { code = "campaign_not_found", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { code = "campaign_closed", message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }

    /// <summary>Historial de instrucciones, más reciente primero.</summary>
    [HttpGet("{campaignId:guid}/prompt/versions")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<IReadOnlyList<PromptVersionResponse>>> ListPromptVersions(
        Guid campaignId, CancellationToken cancellationToken)
    {
        try
        {
            var versions = await campaigns.ListPromptVersionsAsync(campaignId, cancellationToken);
            return versions.Select(v => v.ToResponse()).ToList();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { code = "campaign_not_found", message = ex.Message });
        }
    }

    /// <summary>
    /// Vuelve a aplicar una versión pasada. Esto crea una entrada de historial nueva
    /// (una restauración es un cambio como cualquier otro), no borra ni reescribe nada.
    /// </summary>
    [HttpPost("{campaignId:guid}/prompt/versions/{versionId:guid}/restore")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<PromptUpdateResponse>> RestorePromptVersion(
        Guid campaignId, Guid versionId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await campaigns.RestorePromptVersionAsync(
                campaignId, versionId, currentUser.UserName ?? "desconocido", cancellationToken);
            return result.ToResponse();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { code = "campaign_not_found", message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new { code = "campaign_closed", message = ex.Message });
        }
    }

    /// <summary>
    /// Compara, para la misma pregunta y el mismo contexto recuperado, la respuesta con
    /// las instrucciones publicadas y con un candidato sin guardar. No crea conversación
    /// ni telemetría: es una herramienta de administración, no tráfico real.
    /// </summary>
    [HttpPost("{campaignId:guid}/prompt/preview")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<PromptPreviewResponse>> PreviewPrompt(
        Guid campaignId, [FromBody] PromptPreviewRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Question))
            return BadRequest(new { code = "validation_error", message = "La pregunta de prueba es obligatoria." });

        try
        {
            var candidate = new AssistantPromptRequest(
                request.Tone, request.DetailLevel, request.MandatoryNotice,
                request.AvoidWords, request.ExtraInstructions).ToDomain();

            var result = await promptPreview.PreviewAsync(campaignId, candidate, request.Question, cancellationToken);

            return new PromptPreviewResponse(
                result.CurrentAnswer, result.CandidateAnswer,
                result.Citations.Select(ChatMappings.ToDto).ToList(), result.Warnings);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { code = "campaign_not_found", message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { code = "validation_error", message = ex.Message });
        }
    }
}
