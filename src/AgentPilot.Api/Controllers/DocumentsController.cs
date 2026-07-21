using AgentPilot.Api.Contracts;
using AgentPilot.Application.Abstractions;
using AgentPilot.Application.Ingestion;
using AgentPilot.Domain.Documents;
using Microsoft.AspNetCore.Mvc;

namespace AgentPilot.Api.Controllers;

[ApiController]
[Route("api/v1/documents")]
public class DocumentsController(
    IDocumentIngestionService ingestion,
    IDocumentRepository repository) : ControllerBase
{
    /// <summary>Sube un documento; la ingesta se procesa en segundo plano.</summary>
    [HttpPost]
    public async Task<IActionResult> Upload(
        IFormFile file, [FromForm] string? title, CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { code = "validation_error", message = "El fichero está vacío." });

        try
        {
            await using var stream = file.OpenReadStream();
            var document = await ingestion.SubmitAsync(file.FileName, title, stream, cancellationToken);

            // 202 Accepted: aceptado y en proceso. Location apunta a la consulta de estado.
            return AcceptedAtAction(
                nameof(GetById), new { documentId = document.Id }, document.ToResponse());
        }
        catch (NotSupportedException ex)
        {
            return BadRequest(new { code = "unsupported_format", message = ex.Message });
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

    [HttpDelete("{documentId:guid}")]
    public async Task<IActionResult> Delete(Guid documentId, CancellationToken cancellationToken)
    {
        var document = await repository.GetByIdAsync(documentId, cancellationToken);
        if (document is null) return NotFound();

        repository.Delete(document); // los chunks se borran en cascada
        await repository.SaveChangesAsync(cancellationToken);
        return NoContent();
    }
}
