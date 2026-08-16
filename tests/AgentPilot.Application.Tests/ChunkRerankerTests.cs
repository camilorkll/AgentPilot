using AgentPilot.Application.Retrieval;

namespace AgentPilot.Application.Tests;

public class ChunkRerankerTests
{
    private static ChunkMatch Fragmento(string contenido, double score) => new()
    {
        ChunkId = Guid.NewGuid(),
        DocumentId = Guid.NewGuid(),
        DocumentTitle = "doc",
        Content = contenido,
        Score = score,
    };

    [Fact]
    public void SubeElFragmentoQueContieneLosTerminosDeLaPregunta()
    {
        // El caso real del set dorado: varios fragmentos del mismo catálogo puntúan
        // parecido en vectorial, y el que responde no era el primero.
        var candidatos = new[]
        {
            Fragmento("Tarifa | Datos | Precio. Nova Mini 10 GB 9,90 €. Nova Max 120 GB.", 0.82),
            Fragmento("Los datos no consumidos NO se acumulan al mes siguiente, salvo Nova Max.", 0.80),
        };

        var reordenado = ChunkReranker.Rerank(candidatos, "¿Se acumulan los datos no consumidos?", 2);

        Assert.Contains("NO se acumulan", reordenado[0].Content);
    }

    [Fact]
    public void ConVocabularioDistinto_MandaLaSimilitudVectorial()
    {
        // Si lo léxico pesara demasiado, una pregunta que no comparte palabras con su
        // respuesta se hundiría. El peso vectorial alto es lo que lo evita.
        var candidatos = new[]
        {
            Fragmento("El importe mensual del servicio asciende a 19,90 euros.", 0.90),
            Fragmento("Precio precio precio de otra cosa sin relación alguna.", 0.40),
        };

        var reordenado = ChunkReranker.Rerank(candidatos, "¿Cuál es el precio?", 1);

        Assert.Contains("19,90", reordenado[0].Content);
    }

    [Fact]
    public void RecortaAlNumeroPedido()
    {
        var candidatos = Enumerable.Range(0, 10)
            .Select(i => Fragmento($"fragmento {i}", 0.9 - i * 0.01)).ToList();

        Assert.Equal(3, ChunkReranker.Rerank(candidatos, "fragmento", 3).Count);
    }

    [Fact]
    public void SinCandidatos_NoRompe()
    {
        Assert.Empty(ChunkReranker.Rerank([], "pregunta", 5));
    }

    [Fact]
    public void PreguntaSoloConPalabrasVacias_ConservaElOrdenVectorial()
    {
        var candidatos = new[] { Fragmento("primero", 0.9), Fragmento("segundo", 0.5) };

        // "de la que" no aporta ningún término discriminante: sin señal léxica, no se
        // debe alterar lo que dijo la búsqueda vectorial.
        var reordenado = ChunkReranker.Rerank(candidatos, "de la que", 2);

        Assert.Equal("primero", reordenado[0].Content);
    }

    [Fact]
    public void LosAcentos_NoImpidenElEmparejamiento()
    {
        var candidatos = new[]
        {
            Fragmento("Sin relación con lo preguntado en absoluto.", 0.85),
            Fragmento("La facturacion se emite el dia 1 de cada mes.", 0.84),
        };

        var reordenado = ChunkReranker.Rerank(candidatos, "¿Cuándo se emite la facturación?", 2);

        Assert.Contains("facturacion", reordenado[0].Content);
    }
}
