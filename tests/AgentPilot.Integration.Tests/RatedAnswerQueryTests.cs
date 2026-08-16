using AgentPilot.Application.Abstractions;
using AgentPilot.Application.Feedback;
using AgentPilot.Domain.Conversations;
using AgentPilot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentPilot.Integration.Tests;

/// <summary>
/// El listado de revisión se construye con una consulta que EF Core tiene que traducir
/// entera a SQL: incluye una subconsulta correlacionada (la pregunta anterior a la
/// respuesta valorada) y un `Take` sobre un `join` de tres tablas. Un doble en memoria
/// no ejercita esa traducción y no detectaría un "could not be translated".
/// </summary>
public class RatedAnswerQueryTests(PgVectorFixture fixture) : IClassFixture<PgVectorFixture>
{
    private static readonly Guid CampañaA = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid CampañaB = Guid.Parse("77777777-7777-7777-7777-777777777777");

    private async Task<AgentPilotDbContext> FreshContextAsync()
    {
        var db = fixture.CreateContext();
        await db.Database.ExecuteSqlRawAsync(
            "TRUNCATE campaigns, llm_call_logs, feedback, conversations, documents CASCADE;");
        await db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO campaigns (""Id"", ""Name"", ""Status"", ""CreatedAtUtc"")
            VALUES ({CampañaA}, 'Campaña A', 1, now()), ({CampañaB}, 'Campaña B', 1, now());");
        return db;
    }

    /// <summary>Deja una conversación de dos turnos y valora la respuesta.</summary>
    private static Message SembrarValorada(
        AgentPilotDbContext db, Guid campaña, string pregunta, string respuesta,
        FeedbackRating rating, string? motivo)
    {
        var conversation = new Conversation(campaña);
        conversation.AddUserMessage(pregunta);
        var assistant = conversation.AddAssistantMessage(respuesta, []);
        db.Conversations.Add(conversation);
        db.Feedback.Add(new Feedback(assistant.Id, rating, motivo, "agente"));
        return assistant;
    }

    [Fact]
    public async Task ListRatedAnswers_TraeLaPreguntaElMotivoYLaCampaña()
    {
        await using var db = await FreshContextAsync();
        SembrarValorada(db, CampañaA, "¿Qué descuento puedo ofrecer?", "Hasta un 30%.",
            FeedbackRating.Negative, "No dice que lo autoriza el supervisor");
        await db.SaveChangesAsync();

        IFeedbackRepository repo = new FeedbackRepository(db);
        var resultado = await repo.ListRatedAnswersAsync(new RatedAnswerFilter());

        var fila = Assert.Single(resultado);
        Assert.Equal("¿Qué descuento puedo ofrecer?", fila.Question);
        Assert.Equal("Hasta un 30%.", fila.Answer);
        Assert.Equal("No dice que lo autoriza el supervisor", fila.Comment);
        Assert.Equal("Campaña A", fila.CampaignName);
        Assert.Equal(FeedbackRating.Negative, fila.Rating);
        Assert.Equal("agente", fila.RatedBy);
    }

    [Fact]
    public async Task ListRatedAnswers_TomaLaPreguntaAnteriorYNoOtraDelMismoHilo()
    {
        await using var db = await FreshContextAsync();

        // Dos turnos en la MISMA conversación: la valorada es la segunda respuesta, así
        // que le corresponde la segunda pregunta, no la primera.
        var conversation = new Conversation(CampañaA);
        conversation.AddUserMessage("Primera pregunta");
        conversation.AddAssistantMessage("Primera respuesta", []);
        conversation.AddUserMessage("Segunda pregunta");
        var segunda = conversation.AddAssistantMessage("Segunda respuesta", []);
        db.Conversations.Add(conversation);
        db.Feedback.Add(new Feedback(segunda.Id, FeedbackRating.Negative, null, "agente"));
        await db.SaveChangesAsync();

        IFeedbackRepository repo = new FeedbackRepository(db);
        var fila = Assert.Single(await repo.ListRatedAnswersAsync(new RatedAnswerFilter()));

        Assert.Equal("Segunda pregunta", fila.Question);
        Assert.Equal("Segunda respuesta", fila.Answer);
    }

    [Fact]
    public async Task ListRatedAnswers_FiltraPorValoracionYPorCampaña()
    {
        await using var db = await FreshContextAsync();
        SembrarValorada(db, CampañaA, "P negativa A", "R", FeedbackRating.Negative, null);
        SembrarValorada(db, CampañaA, "P positiva A", "R", FeedbackRating.Positive, null);
        SembrarValorada(db, CampañaB, "P negativa B", "R", FeedbackRating.Negative, null);
        await db.SaveChangesAsync();

        IFeedbackRepository repo = new FeedbackRepository(db);

        Assert.Equal(3, (await repo.ListRatedAnswersAsync(new RatedAnswerFilter())).Count);
        Assert.Equal(2, (await repo.ListRatedAnswersAsync(
            new RatedAnswerFilter(Rating: FeedbackRating.Negative))).Count);

        var soloA = await repo.ListRatedAnswersAsync(
            new RatedAnswerFilter(Rating: FeedbackRating.Negative, CampaignId: CampañaA));
        Assert.Equal("P negativa A", Assert.Single(soloA).Question);
    }

    [Fact]
    public async Task ListRatedAnswers_RespetaElLimiteYDevuelveLasMasRecientesPrimero()
    {
        await using var db = await FreshContextAsync();
        SembrarValorada(db, CampañaA, "La antigua", "R", FeedbackRating.Negative, null);
        await db.SaveChangesAsync();
        await Task.Delay(10); // separa las marcas de tiempo para que el orden sea inequívoco
        SembrarValorada(db, CampañaA, "La reciente", "R", FeedbackRating.Negative, null);
        await db.SaveChangesAsync();

        IFeedbackRepository repo = new FeedbackRepository(db);
        var limitada = await repo.ListRatedAnswersAsync(new RatedAnswerFilter(Limit: 1));

        Assert.Equal("La reciente", Assert.Single(limitada).Question);
    }
}
