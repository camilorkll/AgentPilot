using System.Runtime.CompilerServices;
using System.Text;
using AgentPilot.Application.Abstractions;
using AgentPilot.Application.Ingestion;
using AgentPilot.Domain.Documents;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentPilot.Application.Tests;

/// <summary>
/// Reingerir el mismo fichero duplicaría sus fragmentos en el índice vectorial, así que
/// la ingesta avisa del duplicado y solo lo sustituye si se pide explícitamente.
/// </summary>
public class DocumentIngestionServiceTests
{
    private static Stream Content(string text = "contenido de prueba")
        => new MemoryStream(Encoding.UTF8.GetBytes(text));

    private static (DocumentIngestionService Service, FakeDocuments Repo) Build(params Documento[] existing)
    {
        var repo = new FakeDocuments(existing);
        var service = new DocumentIngestionService(
            repo, new FakeExtractor(), new FakeChunker(), new FakeEmbeddings(),
            new FakeQueue(), NullLogger<DocumentIngestionService>.Instance);
        return (service, repo);
    }

    [Fact]
    public async Task Submit_DeUnFicheroNuevo_LoEncola()
    {
        var (service, repo) = Build();

        var document = await service.SubmitAsync("tarifas.md", null, Content());

        Assert.Equal("tarifas.md", document.FileName);
        Assert.Single(repo.Documents);
        Assert.Empty(repo.Deleted);
    }

    [Fact]
    public async Task Submit_DeUnFicheroYaExistente_AvisaDelDuplicado()
    {
        var existing = new Documento("Tarifas", "tarifas.md");
        var (service, repo) = Build(existing);

        var ex = await Assert.ThrowsAsync<DuplicateDocumentException>(
            () => service.SubmitAsync("tarifas.md", null, Content()));

        Assert.Equal(existing.Id, ex.ExistingDocumentId);
        Assert.Equal("tarifas.md", ex.FileName);
        Assert.Empty(repo.Deleted); // no se toca nada sin confirmación
    }

    [Fact]
    public async Task Submit_ConReemplazo_BorraElAnteriorYEncolaElNuevo()
    {
        var existing = new Documento("Tarifas", "tarifas.md");
        var (service, repo) = Build(existing);

        var document = await service.SubmitAsync(
            "tarifas.md", null, Content(), replaceExisting: true);

        Assert.Contains(existing, repo.Deleted);       // el anterior se elimina
        Assert.NotEqual(existing.Id, document.Id);      // y se crea uno nuevo
    }

    // --- Dobles ---

    private sealed class FakeDocuments(Documento[] existing) : IDocumentRepository
    {
        public List<Documento> Documents { get; } = [];
        public List<Documento> Deleted { get; } = [];

        public Task AddAsync(Documento d, CancellationToken ct = default) { Documents.Add(d); return Task.CompletedTask; }
        public Task<Documento?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(Documents.Concat(existing).FirstOrDefault(d => d.Id == id));
        public Task<Documento?> GetByFileNameAsync(string fileName, CancellationToken ct = default)
            => Task.FromResult(existing.Except(Deleted).FirstOrDefault(d => d.FileName == fileName));
        public Task<IReadOnlyList<Documento>> ListAsync(EstadoIngesta? s = null, CancellationToken ct = default)
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
