using AgentPilot.Api.Contracts;
using AgentPilot.Application.Abstractions;
using AgentPilot.Application.Campaigns;
using AgentPilot.Application.Ingestion;
using AgentPilot.Domain.Documents;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgentPilot.Api.Controllers;

[ApiController]
[Route("api/v1/documents")]
// Todo el controlador exige rol admin, lectura incluida: la pantalla /documents del
// cliente ya lo exigía (adminGuard) y las dos capas deben decir lo mismo. El agente no
// necesita el catálogo: los fragmentos citados le llegan en el chat (SECURITY.md, A01).
[Authorize(Roles = "admin")]
public class DocumentsController(
    IDocumentIngestionService ingestion,
    IDocumentRepository repository,
    CampaignGuard campaignGuard) : ControllerBase
{
    /// <summary>Sube un documento; la ingesta se procesa en segundo plano.</summary>
    [HttpPost]
    public async Task<IActionResult> Upload(
        IFormFile file, [FromForm] Guid? campaignId, [FromForm] string? title,
        [FromForm] bool replace, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { code = "validation_error", message = "El fichero está vacío." });

        // Sin campaña no se ingiere: el documento quedaría fuera del alcance de
        // cualquier consulta, o peor, habría que elegirle una por defecto.
        if (campaignId is null || campaignId == Guid.Empty)
            return BadRequest(new
            {
                code = "validation_error",
                message = "Hay que indicar la campaña de destino (campaignId).",
            });

        try
        {
            await using var stream = file.OpenReadStream();
            var document = await ingestion.SubmitAsync(
                campaignId.Value, file.FileName, title, stream, replace, cancellationToken);

            // 202 Accepted: aceptado y en proceso. Location apunta a la consulta de estado.
            return AcceptedAtAction(
                nameof(GetById), new { documentId = document.Id }, document.ToResponse());
        }
        catch (NotSupportedException ex)
        {
            return BadRequest(new { code = "unsupported_format", message = ex.Message });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { code = "campaign_not_found", message = ex.Message });
        }
        catch (CampaignClosedException ex)
        {
            return Conflict(new { code = "campaign_closed", message = ex.Message });
        }
        catch (DuplicateDocumentException ex)
        {
            // 409: el cliente decide si reemplazar (reenviando con replace = true).
            return Conflict(new
            {
                code = "duplicate_document",
                message = ex.Message,
                documentId = ex.ExistingDocumentId,
                fileName = ex.FileName,
            });
        }
    }

    /// <summary>
    /// Listado de administración. Sin <paramref name="campaignId"/> devuelve los
    /// documentos de todas las campañas: aquí el filtro es una comodidad, no una
    /// frontera. El aislamiento se aplica en la recuperación, donde no existe la
    /// opción de buscar en todas.
    /// </summary>
    [HttpGet]
    public async Task<IReadOnlyList<DocumentResponse>> List(
        [FromQuery] Guid? campaignId, [FromQuery] string? status,
        CancellationToken cancellationToken)
    {
        EstadoIngesta? filter = Enum.TryParse<EstadoIngesta>(status, ignoreCase: true, out var s) ? s : null;
        var documents = await repository.ListAsync(campaignId, filter, cancellationToken);
        return documents.Select(d => d.ToResponse()).ToList();
    }

    [HttpGet("{documentId:guid}")]
    public async Task<ActionResult<DocumentResponse>> GetById(
        Guid documentId, CancellationToken cancellationToken)
    {
        var document = await repository.GetByIdAsync(documentId, cancellationToken);
        return document is null ? NotFound() : document.ToResponse();
    }

    /// <summary>
    /// Devuelve los fragmentos indexados de un documento. Permite al supervisor consultar
    /// qué hay realmente en la base de conocimiento sin tener el fichero original a mano:
    /// se muestran los fragmentos tal como los usa la búsqueda, no el documento completo.
    /// </summary>
    [HttpGet("{documentId:guid}/content")]
    public async Task<ActionResult<DocumentContentResponse>> GetContent(
        Guid documentId, CancellationToken cancellationToken)
    {
        var document = await repository.GetByIdAsync(documentId, cancellationToken);
        return document is null ? NotFound() : document.ToContentResponse();
    }

    [HttpDelete("{documentId:guid}")]
    public async Task<IActionResult> Delete(Guid documentId, CancellationToken cancellationToken)
    {
        var document = await repository.GetByIdAsync(documentId, cancellationToken);
        if (document is null) return NotFound();

        try
        {
            await campaignGuard.ExigirEditableAsync(document.CampaignId, cancellationToken);
        }
        catch (CampaignClosedException ex)
        {
            return Conflict(new { code = "campaign_closed", message = ex.Message });
        }

        repository.Delete(document); // los chunks se borran en cascada
        await repository.SaveChangesAsync(cancellationToken);
        return NoContent();
    }

    /// <summary>
    /// Activa o desactiva uno o varios documentos. Un documento inactivo queda fuera de
    /// las búsquedas —el asistente no puede recuperarlo ni citarlo— pero conserva sus
    /// fragmentos indexados, de modo que reactivarlo es inmediato y sin coste. Está
    /// pensado para información con vigencia, como promociones temporales.
    /// </summary>
    [HttpPost("active")]
    public async Task<ActionResult<IReadOnlyList<DocumentResponse>>> SetActive(
        [FromBody] SetActiveRequest request, CancellationToken cancellationToken)
    {
        if (request.DocumentIds is null || request.DocumentIds.Count == 0)
            return BadRequest(new { code = "validation_error", message = "No se indicó ningún documento." });

        var documents = new List<Domain.Documents.Documento>();
        foreach (var id in request.DocumentIds.Distinct())
        {
            var document = await repository.GetByIdAsync(id, cancellationToken);
            if (document is not null) documents.Add(document);
        }

        // Se valida por campaña (no documento a documento) y antes de tocar nada: o se
        // aplica a todos los seleccionados, o a ninguno.
        try
        {
            foreach (var campaignId in documents.Select(d => d.CampaignId).Distinct())
                await campaignGuard.ExigirEditableAsync(campaignId, cancellationToken);
        }
        catch (CampaignClosedException ex)
        {
            return Conflict(new { code = "campaign_closed", message = ex.Message });
        }

        var updated = new List<DocumentResponse>();
        foreach (var document in documents)
        {
            if (request.IsActive) document.Activar();
            else document.Desactivar();

            updated.Add(document.ToResponse());
        }

        await repository.SaveChangesAsync(cancellationToken);
        return updated;
    }

    /// <summary>
    /// Elimina varios documentos en una sola operación (y sus fragmentos, en cascada).
    /// Se confirma en una única transacción para no dejar la base de conocimiento a medias.
    /// </summary>
    [HttpPost("delete")]
    public async Task<ActionResult<DeleteDocumentsResponse>> DeleteMany(
        [FromBody] DeleteDocumentsRequest request, CancellationToken cancellationToken)
    {
        if (request.DocumentIds is null || request.DocumentIds.Count == 0)
            return BadRequest(new { code = "validation_error", message = "No se indicó ningún documento." });

        var notFound = new List<Guid>();
        var documents = new List<Domain.Documents.Documento>();

        foreach (var id in request.DocumentIds.Distinct())
        {
            var document = await repository.GetByIdAsync(id, cancellationToken);
            if (document is null) notFound.Add(id);
            else documents.Add(document);
        }

        try
        {
            foreach (var campaignId in documents.Select(d => d.CampaignId).Distinct())
                await campaignGuard.ExigirEditableAsync(campaignId, cancellationToken);
        }
        catch (CampaignClosedException ex)
        {
            return Conflict(new { code = "campaign_closed", message = ex.Message });
        }

        foreach (var document in documents)
            repository.Delete(document);

        await repository.SaveChangesAsync(cancellationToken);
        return new DeleteDocumentsResponse(documents.Count, notFound);
    }

    /// <summary>
    /// Reindexa el corpus de una campaña: vuelve a trocear y vectorizar cada documento a
    /// partir del texto guardado en la ingesta, **sin necesitar los ficheros originales**
    /// (ADR-012). Es lo que hay que hacer después de cambiar la estrategia de troceado o
    /// el modelo de embeddings.
    ///
    /// Responde 202 y trabaja en segundo plano por la misma cola que la ingesta: el
    /// corpus entero puede llevar minutos. Mientras tanto los documentos pasan por
    /// `processing` y sus fragmentos anteriores siguen sirviendo hasta que se sustituyen.
    ///
    /// Los documentos ingeridos antes de que se guardara el texto **no se pueden
    /// reindexar** y se devuelven en `skipped` con el motivo, en vez de omitirse en
    /// silencio: para esos hay que volver a subir el fichero.
    /// </summary>
    [HttpPost("reindex")]
    public async Task<ActionResult<ReindexResponse>> Reindex(
        [FromBody] ReindexRequest request, CancellationToken cancellationToken)
    {
        if (request.CampaignId == Guid.Empty)
            return BadRequest(new { code = "validation_error", message = "La campaña es obligatoria." });

        try
        {
            var result = await ingestion.ReindexCampaignAsync(request.CampaignId, cancellationToken);
            return Accepted(result.ToResponse());
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { code = "campaign_not_found", message = ex.Message });
        }
        catch (CampaignClosedException ex)
        {
            return Conflict(new { code = "campaign_closed", message = ex.Message });
        }
    }
}
