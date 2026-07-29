using AgentPilot.Api.Contracts;
using AgentPilot.Application.Abstractions;
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
        IFormFile file, [FromForm] string? title, [FromForm] bool replace,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { code = "validation_error", message = "El fichero está vacío." });

        try
        {
            await using var stream = file.OpenReadStream();
            var document = await ingestion.SubmitAsync(
                file.FileName, title, stream, replace, cancellationToken);

            // 202 Accepted: aceptado y en proceso. Location apunta a la consulta de estado.
            return AcceptedAtAction(
                nameof(GetById), new { documentId = document.Id }, document.ToResponse());
        }
        catch (NotSupportedException ex)
        {
            return BadRequest(new { code = "unsupported_format", message = ex.Message });
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

    [HttpGet]
    public async Task<IReadOnlyList<DocumentResponse>> List(
        [FromQuery] string? status, CancellationToken cancellationToken)
    {
        EstadoIngesta? filter = Enum.TryParse<EstadoIngesta>(status, ignoreCase: true, out var s) ? s : null;
        var documents = await repository.ListAsync(filter, cancellationToken);
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
