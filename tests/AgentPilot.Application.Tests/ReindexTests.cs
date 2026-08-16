using AgentPilot.Application.Abstractions;
using AgentPilot.Application.Campaigns;
using AgentPilot.Application.Ingestion;
using AgentPilot.Domain.Campaigns;
using AgentPilot.Domain.Documents;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentPilot.Application.Tests;

/// <summary>
/// Reindexar existe para no depender de que alguien conserve los ficheros originales
/// (ADR-012). Estas pruebas fijan lo que hace falta para que eso sea cierto: que el
/// texto quede guardado al ingerir, que el reindexado lo use sin fichero, y que los
/// documentos que no lo tengan se informen en vez de fallar en silencio.
/// </summary>
public class ReindexTests
{
    private static readonly Guid IdCampaña = Guid.NewGuid();

    [Fact]
    public async Task AlIngerir_SeGuardaElTextoExtraido()
    {
        var (service, repo, _) = Construir();
        var documento = new Documento(IdCampaña, "Doc", "doc.md");
        repo.Añadir(documento);

        await service.ProcessAsync(new IngestionJob(documento.Id, "doc.md", "El texto del fichero"u8.ToArray()));

        Assert.Equal(EstadoIngesta.Ready, documento.Status);
        Assert.Equal("El texto del fichero", documento.ExtractedText);
        Assert.True(documento.PuedeReindexarse);
    }

    [Fact]
    public async Task Reindexar_VuelveATrocearSinFichero()
    {
        var (service, repo, _) = Construir();
        var documento = new Documento(IdCampaña, "Doc", "doc.md");
        repo.Añadir(documento);
        await service.ProcessAsync(new IngestionJob(documento.Id, "doc.md", "uno dos tres"u8.ToArray()));
        var antes = documento.ChunkCount;

        // El trabajo de reindexado NO lleva bytes: si el servicio intentara extraer del
        // fichero, aquí reventaría.
        await service.ProcessAsync(IngestionJob.Reindexado(documento.Id, "doc.md"));

        Assert.Equal(EstadoIngesta.Ready, documento.Status);
        Assert.Equal(antes, documento.ChunkCount);
        Assert.Equal("uno dos tres", documento.ExtractedText);
    }

    [Fact]
    public async Task ReindexarCampaña_EncolaLosQueTienenTextoYOmiteLosDemas()
    {
        var (service, repo, queue) = Construir();

        var conTexto = new Documento(IdCampaña, "Con texto", "nuevo.md");
        repo.Añadir(conTexto);
        await service.ProcessAsync(new IngestionJob(conTexto.Id, "nuevo.md", "contenido"u8.ToArray()));

        // Documento heredado: indexado antes de que se guardara el texto.
        var sinTexto = new Documento(IdCampaña, "Antiguo", "antiguo.md");
        sinTexto.MarcarProcesando();
        sinTexto.MarcarIndexado("test", [new Chunk(0, "trozo suelto", [0.1f])], "texto");
        BorrarTextoComoSiFueraHeredado(sinTexto);
        repo.Añadir(sinTexto);

        queue.Vaciar();
        var resultado = await service.ReindexCampaignAsync(IdCampaña);

        Assert.Equal(conTexto.Id, Assert.Single(resultado.Encolados));
        var omitido = Assert.Single(resultado.Omitidos);
        Assert.Equal("antiguo.md", omitido.FileName);
        Assert.Contains("volver a subir el fichero", omitido.Motivo);
    }

    [Fact]
    public async Task ReindexarUnDocumentoSinTexto_LanzaConMensajeAccionable()
    {
        var (service, repo, _) = Construir();
        var documento = new Documento(IdCampaña, "Antiguo", "antiguo.md");
        documento.MarcarProcesando();
        documento.MarcarIndexado("test", [new Chunk(0, "trozo", [0.1f])], "texto");
        BorrarTextoComoSiFueraHeredado(documento);
        repo.Añadir(documento);

        // ProcessAsync captura el fallo y lo deja en el documento, no lo propaga.
        await service.ProcessAsync(IngestionJob.Reindexado(documento.Id, "antiguo.md"));

        Assert.Equal(EstadoIngesta.Failed, documento.Status);
        Assert.Contains("volver a subir el fichero", documento.ErrorMessage);
    }

    [Fact]
    public async Task ReindexarUnaCampañaCerrada_SeRechaza()
    {
        var campaña = new Campaña("Cerrada");
        campaña.Desactivar();
        campaña.Cerrar();
        var (service, _, _) = Construir(campaña);

        await Assert.ThrowsAsync<CampaignClosedException>(
            () => service.ReindexCampaignAsync(campaña.Id));
    }

    /// <summary>
    /// Simula un documento anterior a ADR-012. Se hace por reflexión a propósito: el
    /// dominio no ofrece forma de dejar el texto vacío tras indexar, que es justo la
    /// garantía que se quiere.
    /// </summary>
    private static void BorrarTextoComoSiFueraHeredado(Documento documento) =>
        typeof(Documento).GetProperty(nameof(Documento.ExtractedText))!
            .SetValue(documento, null);

    private static (DocumentIngestionService, FakeDocuments, FakeQueue) Construir(Campaña? campaña = null)
    {
        campaña ??= CampañaConId(IdCampaña);
        var repo = new FakeDocuments();
        var queue = new FakeQueue();
        var service = new DocumentIngestionService(
            repo, new FakeExtractor(), new SlidingWindowChunker(), new FakeEmbeddings(), queue,
            new CampaignGuard(new FakeCampaigns(campaña)), NullLogger<DocumentIngestionService>.Instance);
        return (service, repo, queue);
    }

    private static Campaña CampañaConId(Guid id)
    {
        var campaña = new Campaña("De pruebas");
        typeof(Campaña).GetProperty(nameof(Campaña.Id))!.SetValue(campaña, id);
        return campaña;
    }

    // --- Dobles ---

    private sealed class FakeDocuments : IDocumentRepository
    {
        private readonly List<Documento> _docs = [];
        public void Añadir(Documento d) => _docs.Add(d);

        public Task AddAsync(Documento d, CancellationToken ct = default) { _docs.Add(d); return Task.CompletedTask; }
        public Task<Documento?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_docs.FirstOrDefault(d => d.Id == id));
        public Task<Documento?> GetByFileNameAsync(Guid c, string f, CancellationToken ct = default)
            => Task.FromResult(_docs.FirstOrDefault(d => d.CampaignId == c && d.FileName == f));
        public Task<IReadOnlyList<Documento>> ListAsync(
            Guid? campaignId = null, EstadoIngesta? status = null, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Documento>>(
                _docs.Where(d => campaignId is null || d.CampaignId == campaignId).ToList());
        public void Delete(Documento d) => _docs.Remove(d);
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeQueue : IIngestionQueue
    {
        public List<IngestionJob> Trabajos { get; } = [];
        public void Vaciar() => Trabajos.Clear();
        public ValueTask EnqueueAsync(IngestionJob job, CancellationToken ct = default)
        {
            Trabajos.Add(job);
            return ValueTask.CompletedTask;
        }
        public async IAsyncEnumerable<IngestionJob> DequeueAllAsync(
            [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
        {
            foreach (var t in Trabajos) { yield return t; await Task.Yield(); }
        }
    }

    private sealed class FakeExtractor : IDocumentTextExtractor
    {
        public bool Supports(string fileName) => true;
        public Task<string> ExtractTextAsync(Stream s, string f, CancellationToken ct = default)
        {
            using var reader = new StreamReader(s);
            return reader.ReadToEndAsync(ct);
        }
    }

    private sealed class FakeEmbeddings : IEmbeddingService
    {
        public string ModelName => "fake-embed";
        public Task<float[]> EmbedAsync(string text, CancellationToken ct = default)
            => Task.FromResult(new[] { 0.1f });
        public Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<float[]>>(texts.Select(_ => new[] { 0.1f }).ToList());
    }

    private sealed class FakeCampaigns(Campaña campaña) : ICampaignRepository
    {
        public Task<Campaña?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(campaña.Id == id ? campaña : null);

        public Task<IReadOnlyList<CampaignWithCounts>> ListWithCountsAsync(CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<IReadOnlyList<CampaignWithCounts>> ListActiveWithCountsAsync(CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<(int, int)> CountDocumentsAsync(Guid id, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<bool> ExistsByNameAsync(string n, Guid? e = null, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task AddAsync(Campaña c, CancellationToken ct = default) => throw new NotSupportedException();
        public void Delete(Campaña c) => throw new NotSupportedException();
        public Task AddPromptVersionAsync(Domain.Campaigns.PromptVersion v, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<IReadOnlyList<Domain.Campaigns.PromptVersion>> ListPromptVersionsAsync(Guid c, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<Domain.Campaigns.PromptVersion?> GetPromptVersionAsync(Guid c, Guid v, CancellationToken ct = default)
            => throw new NotSupportedException();
        public void DeletePromptVersion(Domain.Campaigns.PromptVersion v) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
