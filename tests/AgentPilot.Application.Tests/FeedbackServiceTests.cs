using AgentPilot.Application.Abstractions;
using AgentPilot.Application.Feedback;
using AgentPilot.Domain.Conversations;
using FeedbackEntity = AgentPilot.Domain.Conversations.Feedback;

namespace AgentPilot.Application.Tests;

public class FeedbackServiceTests
{
    [Fact]
    public async Task Submit_ConMensajeExistente_GuardaElFeedback()
    {
        var repo = new FakeFeedbackRepository(messageExists: true);
        var service = new FeedbackService(repo);
        var messageId = Guid.NewGuid();

        await service.SubmitAsync(messageId, FeedbackRating.Positive, "Muy útil", "agente");

        Assert.NotNull(repo.Saved);
        Assert.Equal(messageId, repo.Saved!.MessageId);
        Assert.Equal(FeedbackRating.Positive, repo.Saved.Rating);
        Assert.Equal("agente", repo.Saved.CreatedBy);
    }

    [Fact]
    public async Task Submit_ConMensajeInexistente_Lanza()
    {
        var repo = new FakeFeedbackRepository(messageExists: false);
        var service = new FeedbackService(repo);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.SubmitAsync(Guid.NewGuid(), FeedbackRating.Negative, null, "agente"));
        Assert.Null(repo.Saved);
    }

    [Fact]
    public async Task Submit_DosVecesElMismoMensaje_RectificaEnVezDeDuplicar()
    {
        var repo = new FakeFeedbackRepository(messageExists: true);
        var service = new FeedbackService(repo);
        var messageId = Guid.NewGuid();

        await service.SubmitAsync(messageId, FeedbackRating.Negative, "No encontró la tarifa", "ana");
        await service.SubmitAsync(messageId, FeedbackRating.Positive, null, "ana");

        // Una sola fila: el porcentaje de respuestas útiles es positivos entre
        // valoradas, así que dos filas contarían esa respuesta dos veces.
        Assert.Equal(1, repo.Altas);
        Assert.Equal(FeedbackRating.Positive, repo.Saved!.Rating);
    }

    [Fact]
    public async Task Submit_AlPasarDeNegativoAPositivo_NoConservaElMotivoAntiguo()
    {
        var repo = new FakeFeedbackRepository(messageExists: true);
        var service = new FeedbackService(repo);
        var messageId = Guid.NewGuid();

        await service.SubmitAsync(messageId, FeedbackRating.Negative, "Se inventó el precio", "ana");
        await service.SubmitAsync(messageId, FeedbackRating.Positive, null, "ana");

        // "Se inventó el precio" describía un rechazo; mantenerlo junto a un pulgar
        // arriba dejaría un motivo que contradice la valoración.
        Assert.Null(repo.Saved!.Comment);
    }

    [Fact]
    public async Task Submit_ConComentarioEnBlanco_LoGuardaComoNulo()
    {
        var repo = new FakeFeedbackRepository(messageExists: true);
        var service = new FeedbackService(repo);

        await service.SubmitAsync(Guid.NewGuid(), FeedbackRating.Negative, "   ", "ana");

        Assert.Null(repo.Saved!.Comment);
    }

    private sealed class FakeFeedbackRepository(bool messageExists) : IFeedbackRepository
    {
        public FeedbackEntity? Saved { get; private set; }
        public int Altas { get; private set; }

        public Task<bool> MessageExistsAsync(Guid id, CancellationToken ct = default) => Task.FromResult(messageExists);

        public Task<FeedbackEntity?> GetByMessageAsync(Guid messageId, CancellationToken ct = default)
            => Task.FromResult(Saved is not null && Saved.MessageId == messageId ? Saved : null);

        public Task AddAsync(FeedbackEntity feedback, CancellationToken ct = default)
        {
            Saved = feedback;
            Altas++;
            return Task.CompletedTask;
        }

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
