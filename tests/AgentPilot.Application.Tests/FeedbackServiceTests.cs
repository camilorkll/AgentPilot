using AgentPilot.Application.Abstractions;
using AgentPilot.Application.Feedback;
using AgentPilot.Domain.Conversations;

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

    private sealed class FakeFeedbackRepository(bool messageExists) : IFeedbackRepository
    {
        public Feedback? Saved { get; private set; }
        public Task<bool> MessageExistsAsync(Guid id, CancellationToken ct = default) => Task.FromResult(messageExists);
        public Task AddAsync(Feedback feedback, CancellationToken ct = default) { Saved = feedback; return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
