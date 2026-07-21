using System.Text;
using AgentPilot.Application.Abstractions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace AgentPilot.Infrastructure.Ai;

/// <summary>
/// Extrae texto de PDF (con PdfPig) y de Markdown/texto plano (lectura directa).
/// Añadir un formato nuevo (p. ej. DOCX) sería otra rama aquí, sin tocar el
/// caso de uso ni el dominio.
/// </summary>
public class DocumentTextExtractor : IDocumentTextExtractor
{
    private static readonly string[] TextExtensions = [".md", ".markdown", ".txt"];

    public bool Supports(string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        return ext == ".pdf" || TextExtensions.Contains(ext);
    }

    public async Task<string> ExtractTextAsync(
        Stream content, string fileName, CancellationToken cancellationToken = default)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();

        if (ext == ".pdf")
        {
            var bytes = await ReadAllBytesAsync(content, cancellationToken);
            return ExtractPdf(bytes);
        }

        if (TextExtensions.Contains(ext))
        {
            using var reader = new StreamReader(content, Encoding.UTF8);
            return await reader.ReadToEndAsync(cancellationToken);
        }

        throw new NotSupportedException($"Formato de fichero no soportado: '{ext}'.");
    }

    private static async Task<byte[]> ReadAllBytesAsync(Stream stream, CancellationToken ct)
    {
        if (stream is MemoryStream ms) return ms.ToArray();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct);
        return buffer.ToArray();
    }

    private static string ExtractPdf(byte[] bytes)
    {
        using var pdf = PdfDocument.Open(bytes);
        var sb = new StringBuilder();
        foreach (var page in pdf.GetPages())
            // ContentOrderTextExtractor reordena el texto en orden de lectura,
            // mejor que el page.Text crudo para documentos con columnas.
            sb.AppendLine(ContentOrderTextExtractor.GetText(page));
        return sb.ToString();
    }
}
