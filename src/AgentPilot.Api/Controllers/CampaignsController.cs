using AgentPilot.Api.Contracts;
using AgentPilot.Application.Abstractions;
using AgentPilot.Application.Campaigns;
using AgentPilot.Application.Chat;
using AgentPilot.Domain.Campaigns;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Numerics;

namespace AgentPilot.Api.Controllers;

[ApiController]
[Route("api/v1/campaigns")]
[Authorize] // /active es de cualquier usuario autenticado; el resto exige admin
public class CampaignsController(
    ICampaignService campaigns, IPromptPreviewService promptPreview, ICurrentUser currentUser) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "admin")]
    // CRK recupera nombre campaña, su estado y la cantidad de documentos indexados, pero no las instrucciones del asistente ni el historial de versiones.
    public async Task<IReadOnlyList<CampaignResponse>> List(CancellationToken cancellationToken)
        => (await campaigns.ListAsync(cancellationToken)).Select(c => c.ToResponse()).ToList();

    /// <sumary>
    /// CRK Devuelve las campañas activas, con su nombre y la cantidad de documentos indexados, para el selector del agente.
    /// No incluye las instrucciones del asistente ni el historial de versiones.
    /// </sumary>
    [HttpGet("active")]    
    public async Task<IReadOnlyList<CampaignSummaryResponse>> ListActive(CancellationToken cancellationToken)
        => (await campaigns.ListActiveAsync(cancellationToken)).Select(c => c.ToSummary()).ToList();

    /// <summary>
    /// CRK Devuelve el nombre de la campaña, su estado y la cantidad de documentos indexados, pero no las instrucciones del asistente ni el historial de versiones. 
    /// </summary>
    /// <param name="campaignId">Identificador de la campaña</param>
    /// <param name="cancellationToken">Token de cancelación</param>
    /// <returns></returns>   
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

    /// <summary>
    /// CRK Crea una campaña activa, sin instrucciones propias. Lanza DuplicateCampaignNameException si el nombre ya existe.
    /// </summary>
    /// <param name="request">Datos de la campaña a crear</param>
    /// <param name="cancellationToken">Token de cancelación</param>
    /// <returns> La campaña creada </returns>
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

    /// <summary>
    /// CRK Renombra la campaña.
    /// Lanza InvalidOperationException si está cerrada ya que una cerrada no se puede renombrar
    /// DuplicateCampaignNameException si el nuevo nombre ya lo usa otra campaña.
    /// Las instrucciones del asistente se gestionan aparte, ver GetPromptAsync/UpdatePromptAsync 
    /// </summary>
    /// <param name="campaignId">Identificador de la campaña</param>
    /// <param name="request">Datos de la campaña a actualizar</param>
    /// <param name="cancellationToken">Token de cancelación</param>
    /// <returns> La campaña actualizada </returns>    
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
    /// CRK Aplica un cambio de estado a la campaña. El cuerpo de la petición indica el nuevo estado.
    /// Lanza InvalidOperationException si la transición no es válida desde el estado actual 
    /// secundario de renombrarla.
    /// </summary>
    /// <param name="campaignId">Identificador de la campaña</param>
    /// <param name="cancellationToken">Token de cancelación</param>
    /// <returns> La campaña actualizada </returns>
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
    /// CRK Elimina la campaña y, en cascada, su corpus. 
    /// Solo permitido si está cerrada: es irreversible y se lleva por delante todo lo indexado.
    /// </summary>
    /// <param name="campaignId">Identificador de la campaña   </param>
    /// <param name="cancellationToken">Token de cancelación</param>
    [HttpDelete("{campaignId:guid}")]
    [Authorize(Roles = "admin")]
    // CRK 
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

    [HttpGet("{campaignId:guid}/prompt")]
    [Authorize(Roles = "admin")]
    // CRK Devuelve las instrucciones vigentes de la campaña para el asistente. No incluye el historial de versiones.
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
    /// CRK Actualiza las instrucciones del asistente para la campaña.
    /// Guardar el formulario vacío equivale a restaurar el comportamiento por defecto (solo el núcleo).
    /// Se controla si la campaña está cerrada: Lanza InvalidOperationException si la campaña está cerrada, y 
    /// ArgumentException si los datos no cumplen las validaciones de dominio.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación</param>
    /// <returns> El prompt actualizado </returns>
    [HttpPut("{campaignId:guid}/prompt")]
    [Authorize(Roles = "admin")]
    // 
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

    /// <summary>CRK Devuelve el historial de instrucciones del asistente para la campaña, más reciente primero.   
    /// No incluye las instrucciones vigentes, que se obtienen en GetPromptAsync.</summary>
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
    /// CRK Restaura una versión pasada de las instrucciones del asistente para la campaña.
    /// Esto crea una entrada de historial nueva (una restauración es un cambio como cualquier otro), no borra ni reescribe nada.
    /// </summary>
    /// <param name="campaignId">ID de la campaña</param>
    /// <param name="versionId">ID de la versión a restaurar</param>
    /// <param name="cancellationToken">Token de cancelación</param>
    /// <returns> El prompt actualizado </returns>

    [HttpPost("{campaignId:guid}/prompt/versions/{versionId:guid}/restore")]
    [Authorize(Roles = "admin")]
    // 
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
    /// Borra una entrada concreta del historial. A diferencia de la purga automática al
    /// superar el límite (que solo se lleva la más antigua), esta la elige el
    /// administrador; no afecta a las instrucciones vigentes de la campaña.
    /// </summary>
    [HttpDelete("{campaignId:guid}/prompt/versions/{versionId:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> DeletePromptVersion(
        Guid campaignId, Guid versionId, CancellationToken cancellationToken)
    {
        try
        {
            await campaigns.DeletePromptVersionAsync(campaignId, versionId, cancellationToken);
            return NoContent();
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
    /// Cambia cuántas entradas conserva el historial de instrucciones de esta campaña.
    /// Si el histórico ya tiene más entradas que el nuevo límite, purga de inmediato las
    /// más antiguas.
    /// </summary>
    [HttpPut("{campaignId:guid}/prompt/max-versions")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<CampaignResponse>> UpdatePromptHistoryLimit(
        Guid campaignId, [FromBody] PromptHistoryLimitRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var updated = await campaigns.UpdateHistoryLimitAsync(campaignId, request.MaxVersions, cancellationToken);
            return updated.ToResponse();
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

    /// <summary>
    /// CRK Compara, para la misma pregunta y el mismo contexto recuperado, la respuesta con
    /// las instrucciones publicadas y con un candidato sin guardar. No crea conversación
    /// ni telemetría: es una herramienta de administración, no tráfico real.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación</param>
    /// <returns> La respuesta actual, la candidata y las citas que se han usado para generar la respuesta candidata</returns>
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
