using AgentPilot.Domain.Documents;

namespace AgentPilot.Domain.Tests;

public class DocumentoTests
{
    [Fact]
    public void NuevoDocumento_ArrancaEnPending()
    {
        var doc = new Documento("Tarifas móviles", "01-tarifas.md");

        Assert.Equal(EstadoIngesta.Pending, doc.Status);
        Assert.Null(doc.ChunkCount);
        Assert.Empty(doc.Chunks);
    }

    [Fact]
    public void SinTitulo_UsaElNombreDeFichero()
    {
        var doc = new Documento("", "01-tarifas.md");

        Assert.Equal("01-tarifas.md", doc.Title);
    }

    [Fact]
    public void FlujoCompleto_PendingProcesandoIndexado()
    {
        var doc = new Documento("Doc", "doc.md");
        var chunks = new[]
        {
            new Chunk(0, "primer fragmento", [0.1f, 0.2f]),
            new Chunk(1, "segundo fragmento", [0.3f, 0.4f]),
        };

        doc.MarcarProcesando();
        doc.MarcarIndexado("text-embedding-3-small", chunks);

        Assert.Equal(EstadoIngesta.Ready, doc.Status);
        Assert.Equal(2, doc.ChunkCount);
        Assert.Equal("text-embedding-3-small", doc.EmbeddingModel);
    }

    [Fact]
    public void Indexar_SinPasarPorProcesando_Falla()
    {
        var doc = new Documento("Doc", "doc.md");
        var chunks = new[] { new Chunk(0, "x", [0.1f]) };

        // No se puede saltar de Pending directamente a Ready
        Assert.Throws<InvalidOperationException>(
            () => doc.MarcarIndexado("modelo", chunks));
    }

    [Fact]
    public void Fallo_RegistraElMotivo_YPermiteReintento()
    {
        var doc = new Documento("Doc", "doc.md");
        doc.MarcarProcesando();

        doc.MarcarFallido("El PDF está corrupto");

        Assert.Equal(EstadoIngesta.Failed, doc.Status);
        Assert.Equal("El PDF está corrupto", doc.ErrorMessage);

        // Desde Failed se puede reintentar (vuelve a Processing y limpia el error)
        doc.MarcarProcesando();
        Assert.Equal(EstadoIngesta.Processing, doc.Status);
        Assert.Null(doc.ErrorMessage);
    }

    [Fact]
    public void Chunk_SinContenido_Falla()
    {
        Assert.Throws<ArgumentException>(() => new Chunk(0, "  ", [0.1f]));
    }

    [Fact]
    public void Chunk_SinEmbedding_Falla()
    {
        Assert.Throws<ArgumentException>(() => new Chunk(0, "texto", []));
    }
}
