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
[Authorize] // cualquier usuario autenticado; la escritura exige rol admin
public class DocumentsController(
    IDocumentIngestionService ingestion,
    IDocumentRepository repository) : ControllerBase
{
    /// <summary>Sube un documento; la ingesta se procesa en segundo plano.</summary>
    [HttpPost]
    [Authorize(Roles = "admin")]
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
    [Authorize(Roles = "admin")]
    public async Task<IActionResult> Delete(Guid documentId, CancellationToken cancellationToken)
    {
        var document = await repository.GetByIdAsync(documentId, cancellationToken);
        if (document is null) return NotFound();

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
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<IReadOnlyList<DocumentResponse>>> SetActive(
        [FromBody] SetActiveRequest request, CancellationToken cancellationToken)
    {
        if (request.DocumentIds is null || request.DocumentIds.Count == 0)
            return BadRequest(new { code = "validation_error", message = "No se indicó ningún documento." });

        var updated = new List<DocumentResponse>();

        foreach (var id in request.DocumentIds.Distinct())
        {
            var document = await repository.GetByIdAsync(id, cancellationToken);
            if (document is null) continue;

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
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<DeleteDocumentsResponse>> DeleteMany(
        [FromBody] DeleteDocumentsRequest request, CancellationToken cancellationToken)
    {
        if (request.DocumentIds is null || request.DocumentIds.Count == 0)
            return BadRequest(new { code = "validation_error", message = "No se indicó ningún documento." });

        var notFound = new List<Guid>();
        var deleted = 0;

        foreach (var id in request.DocumentIds.Distinct())
        {
            var document = await repository.GetByIdAsync(id, cancellationToken);
            if (document is null) { notFound.Add(id); continue; }

            repository.Delete(document);
            deleted++;
        }

        await repository.SaveChangesAsync(cancellationToken);
        return new DeleteDocumentsResponse(deleted, notFound);
    }
}
