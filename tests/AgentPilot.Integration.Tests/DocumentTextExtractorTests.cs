using System.Text;
using AgentPilot.Infrastructure.Ai;

namespace AgentPilot.Integration.Tests;

public class DocumentTextExtractorTests
{
    private readonly DocumentTextExtractor _extractor = new();

    [Theory]
    [InlineData("guia.pdf", true)]
    [InlineData("tarifas.md", true)]
    [InlineData("notas.txt", true)]
    [InlineData("imagen.png", false)]
    [InlineData("hoja.xlsx", false)]
    public void Supports_ReconoceLosFormatosSoportados(string fileName, bool esperado)
    {
        Assert.Equal(esperado, _extractor.Supports(fileName));
    }

    [Fact]
    public async Task ExtractText_DeMarkdown_DevuelveElTexto()
    {
        var markdown = "# Tarifas\n\nNova Mini: 9,90 EUR/mes.";
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(markdown));

        var texto = await _extractor.ExtractTextAsync(stream, "tarifas.md");

        Assert.Contains("Nova Mini", texto);
        Assert.Contains("9,90", texto);
    }

    [Fact]
    public async Task ExtractText_FormatoNoSoportado_Lanza()
    {
        using var stream = new MemoryStream([0x00, 0x01]);

        await Assert.ThrowsAsync<NotSupportedException>(
            () => _extractor.ExtractTextAsync(stream, "archivo.docx"));
    }
}
