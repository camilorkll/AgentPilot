using AgentPilot.Application.Ingestion;

namespace AgentPilot.Application.Tests;

public class SlidingWindowChunkerTests
{
    [Fact]
    public void TextoVacio_NoProduceTrozos()
    {
        var chunker = new SlidingWindowChunker();

        Assert.Empty(chunker.Split(""));
        Assert.Empty(chunker.Split("   \n  "));
    }

    [Fact]
    public void TextoCorto_ProduceUnUnicoTrozo()
    {
        var chunker = new SlidingWindowChunker(chunkSize: 1000, overlap: 200);

        var chunks = chunker.Split("Un texto breve que cabe entero en un trozo.");

        Assert.Single(chunks);
    }

    [Fact]
    public void TextoLargo_TrozosNoExcedenElTamano()
    {
        var chunker = new SlidingWindowChunker(chunkSize: 100, overlap: 20);
        var texto = string.Concat(Enumerable.Repeat("abcdefghij", 50)); // 500 chars

        var chunks = chunker.Split(texto);

        Assert.True(chunks.Count > 1);
        Assert.All(chunks, c => Assert.True(c.Length <= 100));
    }

    [Fact]
    public void TrozosConsecutivos_SeSolapan()
    {
        // Texto sin espacios/puntuación: no hay límites naturales, así que el
        // corte es exacto y el solapamiento es verificable carácter a carácter.
        var chunker = new SlidingWindowChunker(chunkSize: 100, overlap: 20);
        var texto = string.Concat(Enumerable.Repeat("abcdefghijklmnopqrstuvwxyz", 20));

        var chunks = chunker.Split(texto);

        // El final del primer trozo (20 chars) es el principio del segundo.
        Assert.Equal(chunks[0][^20..], chunks[1][..20]);
    }

    [Fact]
    public void Corte_RespetaLimiteNatural()
    {
        var chunker = new SlidingWindowChunker(chunkSize: 50, overlap: 10);
        var primeraLinea = new string('a', 30);
        var texto = primeraLinea + "\n" + new string('b', 60);

        var chunks = chunker.Split(texto);

        // El primer trozo se cierra en el salto de línea (índice 30), no a los 50.
        Assert.True(chunks.Count > 1);
        Assert.Equal(primeraLinea, chunks[0]);
    }

    [Fact]
    public void Constructor_RechazaSolapamientoInvalido()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new SlidingWindowChunker(chunkSize: 100, overlap: 100));
    }
}
