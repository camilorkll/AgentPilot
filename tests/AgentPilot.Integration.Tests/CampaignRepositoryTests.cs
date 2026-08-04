using AgentPilot.Application.Abstractions;
using AgentPilot.Domain.Campaigns;
using AgentPilot.Domain.Documents;
using AgentPilot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentPilot.Integration.Tests;

/// <summary>
/// Prueba lo que solo se puede probar contra un PostgreSQL real: el índice único
/// insensible a mayúsculas y la cascada de borrado que arrastra documentos y
/// fragmentos. Los dobles en memoria de CampaignServiceTests no ejecutan SQL, así
/// que no detectarían una migración que rompiera cualquiera de las dos.
/// </summary>
public class CampaignRepositoryTests(PgVectorFixture fixture) : IClassFixture<PgVectorFixture>
{
    private async Task ResetAsync(AgentPilotDbContext db) =>
        await db.Database.ExecuteSqlRawAsync("TRUNCATE campaigns CASCADE;");

    [Fact]
    public async Task Name_EsUnicoSinDistinguirMayusculas()
    {
        await using var db = fixture.CreateContext();
        await ResetAsync(db);

        db.Campañas.Add(new Campaña("TeleNova"));
        await db.SaveChangesAsync();

        db.Campañas.Add(new Campaña("telenova"));

        // El índice es sobre lower("Name") y vive en SQL (la migración lo crea a mano):
        // el API fluido de EF no puede expresar un índice funcional. Esta es la única
        // prueba que lo ejercita de verdad.
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task EliminarLaCampaña_BorraSusDocumentosYFragmentosEnCascada()
    {
        await using var db = fixture.CreateContext();
        await ResetAsync(db);

        var campaña = new Campaña("Campaña a borrar");
        db.Campañas.Add(campaña);

        var documento = new Documento(campaña.Id, "Doc", "doc.md");
        documento.MarcarProcesando();
        documento.MarcarIndexado(
            "test", [new Chunk(0, "contenido", new float[AgentPilotDbContext.EmbeddingDimensions])]);
        db.Documentos.Add(documento);
        await db.SaveChangesAsync();

        db.Campañas.Remove(campaña);
        await db.SaveChangesAsync();

        Assert.False(await db.Documentos.AnyAsync(d => d.Id == documento.Id));
        Assert.False(await db.Chunks.AnyAsync(c => c.DocumentId == documento.Id));
    }

    /// <summary>
    /// ListWithCountsAsync combina un OrderBy con una proyección que trae subconsultas
    /// de recuento; un doble en memoria (como el de CampaignServiceTests) no ejercita la
    /// traducción a SQL y no habría detectado que EF Core no podía traducir un OrderBy
    /// aplicado DESPUÉS de la proyección. Solo una base de datos real lo demuestra.
    /// </summary>
    [Fact]
    public async Task ListWithCountsAsync_OrdenaYCuentaCorrectamente()
    {
        await using var db = fixture.CreateContext();
        await ResetAsync(db);

        var conDocumentos = new Campaña("Zulu");
        var sinDocumentos = new Campaña("Alfa");
        db.Campañas.AddRange(conDocumentos, sinDocumentos);

        var activo = new Documento(conDocumentos.Id, "Activo", "activo.md");
        activo.MarcarProcesando();
        activo.MarcarIndexado("test", [new Chunk(0, "x", new float[AgentPilotDbContext.EmbeddingDimensions])]);

        var inactivo = new Documento(conDocumentos.Id, "Inactivo", "inactivo.md");
        inactivo.MarcarProcesando();
        inactivo.MarcarIndexado("test", [new Chunk(0, "y", new float[AgentPilotDbContext.EmbeddingDimensions])]);
        inactivo.Desactivar();

        db.Documentos.AddRange(activo, inactivo);
        await db.SaveChangesAsync();

        ICampaignRepository repo = new CampaignRepository(db);
        var lista = await repo.ListWithCountsAsync();

        // Orden alfabético, no de inserción.
        Assert.Equal(["Alfa", "Zulu"], lista.Select(c => c.Campaign.Name));

        var zulu = lista.Single(c => c.Campaign.Name == "Zulu");
        Assert.Equal(2, zulu.DocumentCount);       // total, incluye el inactivo
        Assert.Equal(1, zulu.ActiveDocumentCount); // solo el activo e indexado
    }

    [Fact]
    public async Task EliminarLaCampaña_DejaLaConversacionConCampaignIdANull()
    {
        await using var db = fixture.CreateContext();
        await ResetAsync(db);
        await db.Database.ExecuteSqlRawAsync("TRUNCATE conversations CASCADE;");

        var campaña = new Campaña("Campaña a borrar");
        db.Campañas.Add(campaña);

        var conversacion = new Domain.Conversations.Conversation(campaña.Id);
        conversacion.AddUserMessage("¿pregunta?");
        db.Conversations.Add(conversacion);
        await db.SaveChangesAsync();

        db.Campañas.Remove(campaña);
        await db.SaveChangesAsync();

        // No es corpus, es histórico: la conversación sobrevive, solo pierde la campaña.
        var superviviente = await db.Conversations
            .AsNoTracking()
            .Include(c => c.Messages)
            .FirstAsync(c => c.Id == conversacion.Id);
        Assert.Null(superviviente.CampaignId);
        Assert.NotEmpty(superviviente.Messages);
    }
}
