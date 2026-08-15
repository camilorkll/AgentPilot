using System.Runtime.CompilerServices;
using AgentPilot.Application.Abstractions;
using AgentPilot.Application.Campaigns;
using AgentPilot.Application.Chat;
using AgentPilot.Application.Retrieval;
using AgentPilot.Domain.Campaigns;
using AgentPilot.Domain.Conversations;

namespace AgentPilot.Application.Tests;

public class AskQuestionServiceTests
{
    private static readonly Campaña Activa = new("TeleNova");

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
            embeddings, search, chat, repo, metrics, new FakeCurrentUser("agente"),
            new CampaignGuard(new FakeCampaigns(Activa)));

        // Act
        var events = new List<AskEvent>();
        await foreach (var e in service.AskAsync(
            "¿puedo cambiar de tarifa?", Activa.Id, conversationId: null))
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

        // Se registró la llamada al LLM para el dashboard de coste, atribuida al operador
        // y a la campaña (el nombre va desnormalizado para que el informe sobreviva a
        // que la campaña se elimine).
        Assert.NotNull(metrics.Recorded);
        Assert.Equal("gpt-5-mini", metrics.Recorded!.Model);
        Assert.Equal(120, metrics.Recorded.PromptTokens);
        Assert.Equal("agente", metrics.Recorded.UserName);
        Assert.Equal(Activa.Id, metrics.Recorded.CampaignId);
        Assert.Equal("TeleNova", metrics.Recorded.CampaignName);

        // La conversación queda atada a la campaña.
        Assert.Equal(Activa.Id, saved.CampaignId);
    }

    [Fact]
    public async Task Ask_ComponeElPromptConLasInstruccionesDeLaCampaña()
    {
        // El chat real usa el mismo SystemPromptBuilder que la vista previa: si la
        // campaña tiene instrucciones propias, tienen que llegar al LLM junto al
        // núcleo, no en su lugar.
        var campaña = new Campaña("Luz y Gas Premium");
        campaña.CambiarInstruccionesDelAsistente(
            new AssistantPromptSettings("cercano", null, "Verifica la identidad.", null, null));

        var chat = new FakeChat(["ok"]);
        var service = new AskQuestionService(
            new FakeEmbeddings(), new FakeSearch([]), chat, new FakeConversationRepository(),
            new FakeMetrics(), new FakeCurrentUser("agente"), new CampaignGuard(new FakeCampaigns(campaña)));

        await foreach (var _ in service.AskAsync("¿cuánto cuesta?", campaña.Id, null)) { }

        var system = chat.CapturedMessages.First(m => m.Role == PromptRole.System).Content;
        Assert.Contains("ÚNICAMENTE", system); // el núcleo sigue presente
        Assert.Contains("cercano", system);
        Assert.Contains("Verifica la identidad.", system);
    }

    [Fact]
    public async Task Ask_BuscaSoloEnLaCampañaIndicada()
    {
        var search = new FakeSearch([]);
        var service = Build(search, Activa);

        await foreach (var _ in service.AskAsync("¿cuánto cuesta?", Activa.Id, null)) { }

        // Que la campaña llegue al buscador es la garantía de aislamiento: si se
        // perdiera aquí, el filtro de la consulta SQL no serviría de nada.
        Assert.Equal(Activa.Id, search.CampañaRecibida);
    }

    [Fact]
    public async Task Ask_EnUnaCampañaNoActiva_SeRechaza()
    {
        var inactiva = new Campaña("Campaña en preparación");
        inactiva.Desactivar();
        var service = Build(new FakeSearch([]), inactiva);

        // Se comprueba en cada pregunta: una campaña desactivada a media sesión debe
        // dejar de responder sin esperar a que el agente recargue.
        await Assert.ThrowsAsync<CampaignNotActiveException>(async () =>
        {
            await foreach (var _ in service.AskAsync("¿cuánto cuesta?", inactiva.Id, null)) { }
        });
    }

    [Fact]
    public async Task Ask_ContinuandoUnaConversacionDeOtraCampaña_SeRechaza()
    {
        var repo = new FakeConversationRepository();
        var deOtraCampaña = new Conversation(Guid.NewGuid());
        await repo.AddAsync(deOtraCampaña);

        var service = Build(new FakeSearch([]), Activa, repo);

        // Cambiar de campaña dentro de una conversación arrastraría el historial de la
        // anterior al contexto del modelo, así que exige empezar otra.
        var ex = await Assert.ThrowsAsync<CampaignMismatchException>(async () =>
        {
            await foreach (var _ in service.AskAsync("¿cuánto cuesta?", Activa.Id, deOtraCampaña.Id)) { }
        });

        Assert.Equal(deOtraCampaña.Id, ex.ConversationId);
    }

    [Fact]
    public async Task Ask_ContinuandoUnaConversacionSinCampaña_SeRechaza()
    {
        // Las conversaciones anteriores a las campañas no se pueden continuar: no se
        // sabe con qué corpus se respondieron, así que adoptar una campaña ahora podría
        // mezclar contenidos.
        var repo = new FakeConversationRepository();
        var legado = (Conversation)Activator.CreateInstance(typeof(Conversation), nonPublic: true)!;
        await repo.AddAsync(legado);

        var service = Build(new FakeSearch([]), Activa, repo);

        var ex = await Assert.ThrowsAsync<CampaignMismatchException>(async () =>
        {
            await foreach (var _ in service.AskAsync("¿cuánto cuesta?", Activa.Id, legado.Id)) { }
        });

        Assert.Null(ex.ConversationCampaignId);
        Assert.Contains("anterior a las campañas", ex.Message);
    }

    private static AskQuestionService Build(
        FakeSearch search, Campaña campaña, FakeConversationRepository? repo = null)
        => new(
            new FakeEmbeddings(), search, new FakeChat(["ok"]),
            repo ?? new FakeConversationRepository(), new FakeMetrics(),
            new FakeCurrentUser("agente"), new CampaignGuard(new FakeCampaigns(campaña)));

    // --- Dobles ---

    private sealed class FakeCampaigns(Campaña campaña) : ICampaignRepository
    {
        public Task<Campaña?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(campaña.Id == id ? campaña : null);

        // No ejercitados por estas pruebas: AskQuestionService solo lee una campaña.
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

    private sealed class FakeEmbeddings : IEmbeddingService
    {
        public string ModelName => "fake-embed";
        public Task<float[]> EmbedAsync(string text, CancellationToken ct = default) => Task.FromResult(new[] { 0.1f, 0.2f });
        public Task<IReadOnlyList<float[]>> EmbedBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<float[]>>(texts.Select(_ => new[] { 0.1f, 0.2f }).ToList());
    }

    private sealed class FakeSearch(IReadOnlyList<ChunkMatch> results) : IChunkSearchService
    {
        public Guid? CampañaRecibida { get; private set; }

        public Task<IReadOnlyList<ChunkMatch>> SearchAsync(
            float[] q, Guid campaignId, int topK = 5, CancellationToken ct = default)
        {
            CampañaRecibida = campaignId;
            return Task.FromResult(results);
        }
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
            DateTime? f, DateTime? t, IReadOnlyList<string>? ops = null,
            Metrics.CampaignFilter campaignFilter = default,
            string? monthFromLabel = null, string? monthToLabel = null, CancellationToken ct = default)
            => Task.FromResult(new Metrics.MetricsSummary());
        public Task<IReadOnlyList<string>> GetOperatorsAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<string>>([]);
        public Task<Metrics.MonthRange> ResolveMonthRangeAsync(
            string? monthFrom, string? monthTo, CancellationToken ct = default)
            => throw new NotSupportedException();
        public Task<IReadOnlyList<Metrics.DailyOperatorUsage>> GetDailyByOperatorAsync(
            DateTime? f, DateTime? t, IReadOnlyList<string>? ops = null,
            Metrics.CampaignFilter campaignFilter = default, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<Metrics.DailyOperatorUsage>>([]);
    }

    private sealed class FakeConversationRepository : IConversationRepository
    {
        private readonly Dictionary<Guid, Conversation> _store = [];
        public Task AddAsync(Conversation c, CancellationToken ct = default) { _store[c.Id] = c; return Task.CompletedTask; }
        public Task<Conversation?> GetByIdAsync(Guid id, CancellationToken ct = default) => Task.FromResult(_store.GetValueOrDefault(id));
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
