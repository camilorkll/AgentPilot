using AgentPilot.Domain.Documents;

namespace AgentPilot.Domain.Tests;

public class DocumentoTests
{
    /// <summary>Campaña a la que pertenecen los documentos de estas pruebas.</summary>
    private static readonly Guid Campaña = Guid.NewGuid();

    [Fact]
    public void NuevoDocumento_ArrancaEnPending()
    {
        var doc = new Documento(Campaña, "Tarifas móviles", "01-tarifas.md");

        Assert.Equal(EstadoIngesta.Pending, doc.Status);
        Assert.Equal(Campaña, doc.CampaignId);
        Assert.Null(doc.ChunkCount);
        Assert.Empty(doc.Chunks);
    }

    [Fact]
    public void UnDocumentoSinCampaña_NoTieneSentido()
    {
        // Un documento sin campaña quedaría fuera del alcance de cualquier consulta:
        // indexado, pagado y invisible.
        Assert.Throws<ArgumentException>(
            () => new Documento(Guid.Empty, "Tarifas", "01-tarifas.md"));
    }

    [Fact]
    public void SinTitulo_UsaElNombreDeFichero()
    {
        var doc = new Documento(Campaña, "", "01-tarifas.md");

        Assert.Equal("01-tarifas.md", doc.Title);
    }

    [Fact]
    public void FlujoCompleto_PendingProcesandoIndexado()
    {
        var doc = new Documento(Campaña, "Doc", "doc.md");
        var chunks = new[]
        {
            new Chunk(0, "primer fragmento", [0.1f, 0.2f]),
            new Chunk(1, "segundo fragmento", [0.3f, 0.4f]),
        };

        doc.MarcarProcesando();
        doc.MarcarIndexado("text-embedding-3-small", chunks, "texto del documento");

        Assert.Equal(EstadoIngesta.Ready, doc.Status);
        Assert.Equal(2, doc.ChunkCount);
        Assert.Equal("text-embedding-3-small", doc.EmbeddingModel);
    }

    [Fact]
    public void Indexar_SinPasarPorProcesando_Falla()
    {
        var doc = new Documento(Campaña, "Doc", "doc.md");
        var chunks = new[] { new Chunk(0, "x", [0.1f]) };

        // No se puede saltar de Pending directamente a Ready
        Assert.Throws<InvalidOperationException>(
            () => doc.MarcarIndexado("modelo", chunks, "texto"));
    }

    [Fact]
    public void Fallo_RegistraElMotivo_YPermiteReintento()
    {
        var doc = new Documento(Campaña, "Doc", "doc.md");
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
    public void NuevoDocumento_NaceActivo()
    {
        var doc = new Documento(Campaña, "Promociones", "promos.md");

        Assert.True(doc.IsActive);
    }

    [Fact]
    public void Desactivar_YActivar_AlternanLaDisponibilidad()
    {
        // Caso de uso: una promoción caduca y se retira; más adelante vuelve a estar
        // vigente y se reactiva sin volver a vectorizar el documento.
        var doc = new Documento(Campaña, "Promociones de julio", "promos.md");
        doc.MarcarProcesando();
        doc.MarcarIndexado("text-embedding-3-small", [new Chunk(0, "oferta", [0.1f])], "oferta");

        doc.Desactivar();
        Assert.False(doc.IsActive);
        Assert.Single(doc.Chunks);                       // los fragmentos se conservan
        Assert.Equal(EstadoIngesta.Ready, doc.Status);   // y sigue indexado

        doc.Activar();
        Assert.True(doc.IsActive);
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
