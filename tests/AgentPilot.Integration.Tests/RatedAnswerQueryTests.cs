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
        FeedbackRating rating, string? motivo, string? operador = "agente")
    {
        var conversation = new Conversation(campaña, operador);
        conversation.AddUserMessage(pregunta);
        var assistant = conversation.AddAssistantMessage(respuesta, []);
        db.Conversations.Add(conversation);
        db.Feedback.Add(new Feedback(assistant.Id, rating, motivo, operador));
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
        //
        // Los turnos se separan en el tiempo a propósito. El orden de los mensajes se
        // establece por su marca de tiempo, y DateTime.UtcNow tiene resolución de
        // milisegundos: creados de golpe, los cuatro comparten marca y el orden entre
        // ellos deja de estar definido. En producción no pasa —entre una pregunta y su
        // respuesta media la llamada al modelo, cientos de milisegundos como mínimo—,
        // así que el retardo hace el test realista, no indulgente.
        var conversation = new Conversation(CampañaA, "agente");
        conversation.AddUserMessage("Primera pregunta");
        await Task.Delay(5);
        conversation.AddAssistantMessage("Primera respuesta", []);
        await Task.Delay(5);
        conversation.AddUserMessage("Segunda pregunta");
        await Task.Delay(5);
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
    public async Task ListRatedAnswers_FiltraPorOperadorQueMantuvoLaConversacion()
    {
        await using var db = await FreshContextAsync();
        SembrarValorada(db, CampañaA, "Pregunta de Ana", "R", FeedbackRating.Negative, null, "ana");
        SembrarValorada(db, CampañaA, "Pregunta de Luis", "R", FeedbackRating.Negative, null, "luis");
        await db.SaveChangesAsync();

        IFeedbackRepository repo = new FeedbackRepository(db);
        var deAna = await repo.ListRatedAnswersAsync(new RatedAnswerFilter(Operator: "ana"));

        var fila = Assert.Single(deAna);
        Assert.Equal("Pregunta de Ana", fila.Question);
        Assert.Equal("ana", fila.Operator);
    }

    [Fact]
    public async Task ListRatedAnswers_DistingueQuienConversoDeQuienValoro()
    {
        await using var db = await FreshContextAsync();

        // Un administrador valora la respuesta de una conversación que mantuvo Ana: el
        // listado tiene que poder atribuir cada cosa a quien corresponde.
        var conversation = new Conversation(CampañaA, "ana");
        conversation.AddUserMessage("Pregunta de Ana");
        var respuesta = conversation.AddAssistantMessage("Respuesta", []);
        db.Conversations.Add(conversation);
        db.Feedback.Add(new Feedback(respuesta.Id, FeedbackRating.Negative, "revisado", "admin"));
        await db.SaveChangesAsync();

        IFeedbackRepository repo = new FeedbackRepository(db);
        var fila = Assert.Single(await repo.ListRatedAnswersAsync(new RatedAnswerFilter()));

        Assert.Equal("ana", fila.Operator);   // quien conversó
        Assert.Equal("admin", fila.RatedBy);  // quien valoró
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
