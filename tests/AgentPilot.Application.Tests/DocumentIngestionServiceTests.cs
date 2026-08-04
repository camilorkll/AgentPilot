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
    public async Task Submit_ConReemplazo_BorraElAnteriorYEncolaElNuevo()
    {
        var existing = new Documento(Activa.Id, "Tarifas", "tarifas.md");
        var (service, repo) = Build(Activa, existing);

        var document = await service.SubmitAsync(
            Activa.Id, "tarifas.md", null, Content(), replaceExisting: true);

        Assert.Contains(existing, repo.Deleted);       // el anterior se elimina
        Assert.NotEqual(existing.Id, document.Id);      // y se crea uno nuevo
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
