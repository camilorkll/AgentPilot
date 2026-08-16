using System.Runtime.CompilerServices;
using System.Text;
using AgentPilot.Application.Abstractions;
using AgentPilot.Application.Campaigns;
using AgentPilot.Application.Ingestion;
using AgentPilot.Domain.Campaigns;
using AgentPilot.Domain.Documents;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentPilot.Application.Tests;

/// <summary>
/// Reingerir el mismo fichero duplicaría sus fragmentos en el índice vectorial, así que
/// la ingesta avisa del duplicado y solo lo sustituye si se pide explícitamente. Además
/// no acepta nada sin una campaña válida y editable.
/// </summary>
public class DocumentIngestionServiceTests
{
    private static readonly Campaña Activa = new("TeleNova");

    private static Stream Content(string text = "contenido de prueba")
        => new MemoryStream(Encoding.UTF8.GetBytes(text));

    private static (DocumentIngestionService Service, FakeDocuments Repo) Build(
        Campaña? campaña = null, params Documento[] existing)
    {
        var repo = new FakeDocuments(existing);
        var service = new DocumentIngestionService(
            repo, new FakeExtractor(), new FakeChunker(), new FakeEmbeddings(),
            new FakeQueue(), new CampaignGuard(new FakeCampaigns(campaña ?? Activa)),
            NullLogger<DocumentIngestionService>.Instance);
        return (service, repo);
    }

    /// <summary>Como <see cref="Build"/>, pero con el proveedor de embeddings caído.</summary>
    private static (DocumentIngestionService Service, FakeDocuments Repo) BuildConIngestaRota(
        params Documento[] existing)
    {
        var repo = new FakeDocuments(existing);
        var service = new DocumentIngestionService(
            repo, new FakeExtractor(), new FakeChunker(), new FakeEmbeddingsRotos(),
            new FakeQueue(), new CampaignGuard(new FakeCampaigns(Activa)),
            NullLogger<DocumentIngestionService>.Instance);
        return (service, repo);
    }

    private sealed class FakeEmbeddingsRotos : IEmbeddingService
    {
        public string ModelName => "fake-embed";
        public Task<float[]> EmbedAsync(string t, CancellationToken ct = default)
            => throw new HttpRequestException("El proveedor de embeddings no responde.");
        public Task<IReadOnlyList<float[]>> EmbedBatchAsync(
            IReadOnlyList<string> texts, CancellationToken ct = default)
            => throw new HttpRequestException("El proveedor de embeddings no responde.");
    }

    [Fact]
    public async Task Submit_DeUnFicheroNuevo_LoEncolaEnLaCampaña()
    {
        var (service, repo) = Build();

        var document = await service.SubmitAsync(Activa.Id, "tarifas.md", null, Content());

        Assert.Equal("tarifas.md", document.FileName);
        Assert.Equal(Activa.Id, document.CampaignId);
        Assert.Single(repo.Documents);
        Assert.Empty(repo.Deleted);
    }

    [Fact]
    public async Task Submit_DeUnFicheroYaExistente_AvisaDelDuplicado()
    {
        var existing = new Documento(Activa.Id, "Tarifas", "tarifas.md");
        var (service, repo) = Build(Activa, existing);

        var ex = await Assert.ThrowsAsync<DuplicateDocumentException>(
            () => service.SubmitAsync(Activa.Id, "tarifas.md", null, Content()));

        Assert.Equal(existing.Id, ex.ExistingDocumentId);
        Assert.Equal("tarifas.md", ex.FileName);
        Assert.Empty(repo.Deleted); // no se toca nada sin confirmación
    }

    [Fact]
    public async Task Submit_ConReemplazo_ReprocesaLaMismaFilaSinBorrarNada()
    {
        var existing = new Documento(Activa.Id, "Tarifas", "tarifas.md");
        var (service, repo) = Build(Activa, existing);

        var document = await service.SubmitAsync(
            Activa.Id, "tarifas.md", "Tarifas 2027", Content(), replaceExisting: true);

        // Antes se borraba el anterior ANTES de encolar el nuevo, así que un fallo de la
        // ingesta dejaba la campaña sin ese conocimiento y sin vuelta atrás.
        Assert.Empty(repo.Deleted);
        Assert.Equal(existing.Id, document.Id);        // misma fila: las citas emitidas siguen apuntando a algo
        Assert.Equal("Tarifas 2027", document.Title);  // el título sí se actualiza
    }

    [Fact]
    public async Task Reemplazo_QueFalla_DejaServibleLaVersionAnterior()
    {
        var existing = new Documento(Activa.Id, "Tarifas", "tarifas.md");
        existing.MarcarProcesando();
        existing.MarcarIndexado("test", [new Chunk(0, "Nova Mini: 9,90 €", [0.1f])], "Nova Mini: 9,90 €");

        // Embeddings caídos: el fallo más probable de verdad (proveedor no disponible,
        // límite de peticiones), y el que antes se llevaba por delante el documento.
        var (service, repo) = BuildConIngestaRota(existing);

        await service.SubmitAsync(Activa.Id, "tarifas.md", null, Content(), replaceExisting: true);
        await service.ProcessAsync(new IngestionJob(existing.Id, "tarifas.md", "nuevo"u8.ToArray()));

        // Lo que falló es la ACTUALIZACIÓN, no el documento: sigue consultable con su
        // contenido anterior, y el motivo queda registrado para el administrador.
        Assert.Empty(repo.Deleted);
        Assert.Equal(EstadoIngesta.Ready, existing.Status);
        Assert.Equal(1, existing.ChunkCount);
        Assert.Contains("Nova Mini", existing.Chunks.First().Content);
        Assert.True(existing.ActualizacionFallidaConContenidoAnterior);
    }

    [Fact]
    public async Task PrimeraIngestaQueFalla_SeQuedaEnFallido()
    {
        var (service, _) = BuildConIngestaRota();
        var document = await service.SubmitAsync(Activa.Id, "tarifas.md", null, Content());

        await service.ProcessAsync(new IngestionJob(document.Id, "tarifas.md", "nuevo"u8.ToArray()));

        // Aquí no hay nada anterior que preservar, así que el estado honesto es Failed.
        Assert.Equal(EstadoIngesta.Failed, document.Status);
        Assert.Empty(document.Chunks);
        Assert.NotNull(document.ErrorMessage);
    }

    [Fact]
    public void UnDocumentoInterrumpido_VuelveAServirSiTeniaContenido()
    {
        // Lo que hace el rescate de arranque con los documentos que quedaron en
        // 'Processing' al reiniciarse la aplicación.
        var conContenido = new Documento(Activa.Id, "Tarifas", "tarifas.md");
        conContenido.MarcarProcesando();
        conContenido.MarcarIndexado("test", [new Chunk(0, "contenido", [0.1f])], "contenido");
        conContenido.MarcarProcesando(); // reingesta interrumpida por el reinicio

        var sinContenido = new Documento(Activa.Id, "Nuevo", "nuevo.md");
        sinContenido.MarcarProcesando(); // primera ingesta interrumpida

        conContenido.MarcarFallido("La ingesta se interrumpió al reiniciarse la aplicación.");
        sinContenido.MarcarFallido("La ingesta se interrumpió al reiniciarse la aplicación.");

        Assert.Equal(EstadoIngesta.Ready, conContenido.Status);   // su versión anterior sigue valiendo
        Assert.Equal(EstadoIngesta.Failed, sinContenido.Status);  // no hay nada que servir
    }

    [Fact]
    public async Task Submit_ElMismoFicheroEnOtraCampaña_NoEsDuplicado()
    {
        // El duplicado se mira dentro de la campaña: dos campañas pueden tener su
        // propio "tarifas.md" con contenido distinto, y son corpus independientes.
        var deOtraCampaña = new Documento(Guid.NewGuid(), "Tarifas", "tarifas.md");
        var (service, _) = Build(Activa, deOtraCampaña);

        var document = await service.SubmitAsync(Activa.Id, "tarifas.md", null, Content());

        Assert.Equal(Activa.Id, document.CampaignId);
    }

    [Fact]
    public async Task Submit_EnUnaCampañaCerrada_SeRechaza()
    {
        var cerrada = new Campaña("Campaña del año pasado");
        cerrada.Desactivar();
        cerrada.Cerrar();
        var (service, repo) = Build(cerrada);

        var ex = await Assert.ThrowsAsync<CampaignClosedException>(
            () => service.SubmitAsync(cerrada.Id, "tarifas.md", null, Content()));

        Assert.Equal(cerrada.Id, ex.CampaignId);
        // Y no se acepta a medias: nada llega al repositorio ni a la cola.
        Assert.Empty(repo.Documents);
    }

    [Fact]
    public async Task Submit_EnUnaCampañaQueNoExiste_SeRechaza()
    {
        var repo = new FakeDocuments([]);
        var service = new DocumentIngestionService(
            repo, new FakeExtractor(), new FakeChunker(), new FakeEmbeddings(),
            new FakeQueue(), new CampaignGuard(new FakeCampaigns(null)),
            NullLogger<DocumentIngestionService>.Instance);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.SubmitAsync(Guid.NewGuid(), "tarifas.md", null, Content()));

        Assert.Empty(repo.Documents);
    }

    // --- Dobles ---

    private sealed class FakeCampaigns(Campaña? campaña) : ICampaignRepository
    {
        public Task<Campaña?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(campaña is not null && campaña.Id == id ? campaña : null);

        // No ejercitados por estas pruebas: la ingesta solo lee una campaña.
        public Task<IReadOnlyList<CampaignWithCounts>> ListWithCountsAsync(CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<IReadOnlyList<CampaignWithCounts>> ListActiveWithCountsAsync(CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<(int, int)> CountDocumentsAsync(Guid id, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<bool> ExistsByNameAsync(string name, Guid? excludingId = null, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task AddAsync(Campaña c, CancellationToken ct = default) => throw new NotSupportedException();
        public void Delete(Campaña c) => throw new NotSupportedException();
        public Task AddPromptVersionAsync(PromptVersion v, CancellationToken ct = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<PromptVersion>> ListPromptVersionsAsync(Guid campaignId, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<PromptVersion?> GetPromptVersionAsync(Guid campaignId, Guid versionId, CancellationToken ct = default)
            => throw new NotSupportedException();
        public void DeletePromptVersion(PromptVersion version) => throw new NotSupportedException();
        public Task SaveChangesAsync(CancellationToken ct = default) => throw new NotSupportedException();
    }

    private sealed class FakeDocuments(Documento[] existing) : IDocumentRepository
    {
        public List<Documento> Documents { get; } = [];
        public List<Documento> Deleted { get; } = [];

        public Task AddAsync(Documento d, CancellationToken ct = default) { Documents.Add(d); return Task.CompletedTask; }
        public Task<Documento?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Documents.Concat(existing).FirstOrDefault(d => d.Id == id));
        public Task<Documento?> GetByFileNameAsync(Guid campaignId, string fileName, CancellationToken ct = default)
            => Task.FromResult(existing.Except(Deleted)
                .FirstOrDefault(d => d.CampaignId == campaignId && d.FileName == fileName));
        public Task<IReadOnlyList<Documento>> ListAsync(
            Guid? campaignId = null, EstadoIngesta? s = null, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Documento>>(Documents);
        public void Delete(Documento d) => Deleted.Add(d);
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }

    private sealed class FakeExtractor : IDocumentTextExtractor
    {
        public bool Supports(string fileName) => true;
        public Task<string> ExtractTextAsync(Stream c, string f, CancellationToken ct = default)
            => Task.FromResult("texto");
    }

    private sealed class FakeChunker : ITextChunker
    {
        public IReadOnlyList<string> Split(string text) => [text];
    }

    private sealed class FakeEmbeddings : IEmbeddingService
    {
        public string ModelName => "fake-embed";
        public Task<float[]> EmbedAsync(string t, CancellationToken ct = default) => Task.FromResult(new[] { 0.1f });
        public Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> t, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<float[]>>(t.Select(_ => new[] { 0.1f }).ToList());
    }

    private sealed class FakeQueue : IIngestionQueue
    {
        public ValueTask EnqueueAsync(IngestionJob job, CancellationToken ct = default) => ValueTask.CompletedTask;
        public async IAsyncEnumerable<IngestionJob> DequeueAllAsync(
            [EnumeratorCancellation] CancellationToken ct) { await Task.CompletedTask; yield break; }
    }
}
