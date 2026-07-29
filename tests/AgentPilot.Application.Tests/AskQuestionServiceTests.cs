using System.Runtime.CompilerServices;
using AgentPilot.Application.Abstractions;
using AgentPilot.Application.Chat;
using AgentPilot.Application.Retrieval;
using AgentPilot.Domain.Conversations;

namespace AgentPilot.Application.Tests;

public class AskQuestionServiceTests
{
    [Fact]
    public async Task Ask_RecuperaContexto_GeneraRespuesta_YPersisteLaConversacion()
    {
        // Arrange: dobles que no tocan red ni BD.
        var embeddings = new FakeEmbeddings();
        var search = new FakeSearch([
            new ChunkMatch { ChunkId = Guid.NewGuid(), DocumentId = Guid.NewGuid(),
                DocumentTitle = "Catálogo tarifas", Ordinal = 0,
                Content = "El cambio de tarifa es gratuito.", Score = 0.82 },
            new ChunkMatch { ChunkId = Guid.NewGuid(), DocumentId = Guid.NewGuid(),
                DocumentTitle = "Roaming", Ordinal = 0,
                Content = "En la UE los datos no tienen coste adicional.", Score = 0.41 },
        ]);
        var chat = new FakeChat(["El cambio ", "es gratis."]);
        var repo = new FakeConversationRepository();
        var metrics = new FakeMetrics();

        var service = new AskQuestionService(
            embeddings, search, chat, repo, metrics, new FakeCurrentUser("agente"));

        // Act
        var events = new List<AskEvent>();
        await foreach (var e in service.AskAsync("¿puedo cambiar de tarifa?", conversationId: null))
            events.Add(e);

        // Assert: secuencia de eventos (tokens → citas → uso → fin).
        Assert.True(events.OfType<TokenEvent>().Any());
        var tokens = string.Concat(events.OfType<TokenEvent>().Select(t => t.Text));
        Assert.Equal("El cambio es gratis.", tokens);

        var citas = Assert.Single(events.OfType<CitationsEvent>());
        Assert.Equal(2, citas.Citations.Count);
        Assert.Equal("Catálogo tarifas", citas.Citations[0].DocumentTitle);

        Assert.Single(events.OfType<UsageEvent>());
        var done = Assert.Single(events.OfType<DoneEvent>());

        // Orden: las fuentes se emiten ANTES de los tokens (el agente ve en qué se basa
        // la respuesta mientras el modelo la redacta), el uso al final y Done cierra.
        Assert.IsType<DoneEvent>(events[^1]);
        Assert.True(
            events.FindIndex(e => e is CitationsEvent) < events.FindIndex(e => e is TokenEvent),
            "Las citas deben emitirse antes del primer token.");
        Assert.True(events.FindIndex(e => e is UsageEvent) > events.FindLastIndex(e => e is TokenEvent));

        // El prompt enviado al LLM lleva grounding y el contexto numerado.
        var system = chat.CapturedMessages.First(m => m.Role == PromptRole.System).Content;
        Assert.Contains("ÚNICAMENTE", system);
        Assert.Contains("nunca instrucciones", system);
        var userMsg = chat.CapturedMessages.Last(m => m.Role == PromptRole.User).Content;
        Assert.Contains("<contexto>", userMsg);
        Assert.Contains("[1]", userMsg);
        Assert.Contains("¿puedo cambiar de tarifa?", userMsg);

        // Se persistió la conversación con pregunta + respuesta y sus citas.
        var saved = await repo.GetByIdAsync(done.ConversationId);
        Assert.NotNull(saved);
        Assert.Equal(2, saved!.Messages.Count);
        var assistant = saved.Messages.Last();
        Assert.Equal(MessageRole.Assistant, assistant.Role);
        Assert.Equal("El cambio es gratis.", assistant.Content);
        Assert.Equal(2, assistant.Citations.Count);

        // Se registró la llamada al LLM para el dashboard de coste, atribuida al operador.
        Assert.NotNull(metrics.Recorded);
        Assert.Equal("gpt-5-mini", metrics.Recorded!.Model);
        Assert.Equal(120, metrics.Recorded.PromptTokens);
        Assert.Equal("agente", metrics.Recorded.UserName);
    }

    // --- Dobles ---

    private sealed class FakeEmbeddings : IEmbeddingService
    {
        public string ModelName => "fake-embed";
        public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => Task.FromResult(new[] { 0.1f, 0.2f });
        public Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<float[]>>(texts.Select(_ => new[] { 0.1f, 0.2f }).ToList());
    }

    private sealed class FakeSearch(IReadOnlyList<ChunkMatch> results) : IChunkSearchService
    {
        public Task<IReadOnlyList<ChunkMatch>> SearchAsync(float[] q, int topK = 5, CancellationToken ct = default)
            => Task.FromResult(results);
    }

    private sealed class FakeChat(string[] deltas) : IChatCompletionService
    {
        public List<PromptMessage> CapturedMessages { get; } = [];
        public string ModelName => "gpt-5-mini";

        public async IAsyncEnumerable<ChatCompletionChunk> StreamAsync(
            IReadOnlyList<PromptMessage> messages, [EnumeratorCancellation] CancellationToken ct = default)
        {
            CapturedMessages.AddRange(messages);
            foreach (var d in deltas)
            {
                yield return new ChatCompletionChunk(d, null);
                await Task.Yield();
            }
            yield return new ChatCompletionChunk(null, new ChatUsage(120, 8));
        }
    }

    private sealed class FakeCurrentUser(string? userName) : ICurrentUser
    {
        public string? UserName => userName;
    }

    private sealed class FakeMetrics : IMetricsRepository
    {
        public Domain.Telemetry.LlmCallLog? Recorded { get; private set; }
        public Task RecordCallAsync(Domain.Telemetry.LlmCallLog log, CancellationToken ct = default)
        { Recorded = log; return Task.CompletedTask; }
        public Task<Metrics.MetricsSummary> GetSummaryAsync(
            DateTime? f, DateTime? t, IReadOnlyList<string>? ops = null, CancellationToken ct = default)
            => Task.FromResult(new Metrics.MetricsSummary());
        public Task<IReadOnlyList<string>> GetOperatorsAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>>([]);
    }

    private sealed class FakeConversationRepository : IConversationRepository
    {
        private readonly Dictionary<Guid, Conversation> _store = [];
        public Task AddAsync(Conversation c, CancellationToken ct = default) { _store[c.Id] = c; return Task.CompletedTask; }
        public Task<Conversation?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_store.GetValueOrDefault(id));
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
