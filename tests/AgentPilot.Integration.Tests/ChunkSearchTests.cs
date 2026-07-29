using AgentPilot.Domain.Documents;
using AgentPilot.Infrastructure.Ai;
using AgentPilot.Infrastructure.Configuration;
using AgentPilot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;

namespace AgentPilot.Integration.Tests;

public class ChunkSearchTests(PgVectorFixture fixture, ITestOutputHelper output)
    : IClassFixture<PgVectorFixture>
{
    private static string? ApiKey => Environment.GetEnvironmentVariable("OPENAI_API_KEY");

    private static float[] UnitVector(int index)
    {
        var v = new float[AgentPilotDbContext.EmbeddingDimensions];
        v[index] = 1f;
        return v;
    }

    private async Task ResetAsync(AgentPilotDbContext db) =>
        await db.Database.ExecuteSqlRawAsync("TRUNCATE documents CASCADE;");

    [Fact]
    public async Task Busqueda_DevuelvePrimeroElChunkMasCercano()
    {
        await using var db = fixture.CreateContext();
        await ResetAsync(db);

        // Dos chunks "ortogonales": uno apunta a la dimensión 0, otro a la 1.
        var doc = new Documento("Doc", "doc.md");
        doc.MarcarProcesando();
        doc.MarcarIndexado("test", [
            new Chunk(0, "fragmento en la dimensión 0", UnitVector(0)),
            new Chunk(1, "fragmento en la dimensión 1", UnitVector(1)),
        ]);
        db.Documentos.Add(doc);
        await db.SaveChangesAsync();

        // La consulta apunta a la dimensión 0: debe recuperar ese chunk primero.
        var search = new ChunkSearchService(db);
        var results = await search.SearchAsync(UnitVector(0), topK: 2);

        Assert.Equal(2, results.Count);
        Assert.Equal("fragmento en la dimensión 0", results[0].Content);
        Assert.True(results[0].Score > results[1].Score);
        Assert.True(results[0].Score > 0.99); // coseno con sí mismo ≈ 1
    }

    [Fact]
    public async Task Busqueda_IgnoraDocumentosNoIndexados()
    {
        await using var db = fixture.CreateContext();
        await ResetAsync(db);

        // Documento en Processing (sin indexar): no debe aparecer en resultados.
        var enProceso = new Documento("Pendiente", "p.md");
        enProceso.MarcarProcesando();
        db.Documentos.Add(enProceso);
        await db.SaveChangesAsync();

        var results = await new ChunkSearchService(db).SearchAsync(UnitVector(0), topK: 5);

        Assert.Empty(results);
    }

    [Fact]
    public async Task Busqueda_IgnoraDocumentosDesactivados()
    {
        await using var db = fixture.CreateContext();
        await ResetAsync(db);

        // Dos documentos indexados con el mismo vector; uno se desactiva (p. ej. una
        // promoción caducada): su contenido debe desaparecer de los resultados.
        var vigente = new Documento("Tarifas vigentes", "tarifas.md");
        vigente.MarcarProcesando();
        vigente.MarcarIndexado("test", [new Chunk(0, "tarifa vigente", UnitVector(0))]);

        var caducado = new Documento("Promoción caducada", "promos.md");
        caducado.MarcarProcesando();
        caducado.MarcarIndexado("test", [new Chunk(0, "promoción caducada", UnitVector(0))]);
        caducado.Desactivar();

        db.Documentos.AddRange(vigente, caducado);
        await db.SaveChangesAsync();

        var results = await new ChunkSearchService(db).SearchAsync(UnitVector(0), topK: 5);

        var contenidos = results.Select(r => r.Content).ToList();
        Assert.Contains("tarifa vigente", contenidos);
        Assert.DoesNotContain("promoción caducada", contenidos);

        // Al reactivarlo, vuelve a estar disponible sin volver a vectorizar.
        caducado.Activar();
        await db.SaveChangesAsync();

        var afterReactivation = await new ChunkSearchService(db).SearchAsync(UnitVector(0), topK: 5);
        Assert.Contains("promoción caducada", afterReactivation.Select(r => r.Content));
    }

    [SkippableFact]
    public async Task Recuperacion_EncuentraElChunkRelevante_AunqueNoCompartaPalabras()
    {
        Skip.If(string.IsNullOrWhiteSpace(ApiKey),
            "Sin OPENAI_API_KEY: se omite el test de recuperación semántica real.");

        await using var db = fixture.CreateContext();
        await ResetAsync(db);

        var embeddings = new OpenAiEmbeddingService(Options.Create(
            new OpenAiOptions { ApiKey = ApiKey }));

        // Ingerimos tres fragmentos reales del corpus (con embeddings reales).
        var textos = new[]
        {
            "El cambio de tarifa es gratuito y puede hacerse una vez por ciclo de facturación.",
            "En la Unión Europea las llamadas y los datos se consumen de la tarifa nacional sin coste.",
            "Si la luz LOS del router está en rojo, revisa que el cable de fibra no esté desconectado.",
        };
        var vectors = await embeddings.EmbedBatchAsync(textos);

        var doc = new Documento("Base de conocimiento", "kb.md");
        doc.MarcarProcesando();
        doc.MarcarIndexado(embeddings.ModelName,
            textos.Select((t, i) => new Chunk(i, t, vectors[i])).ToList());
        db.Documentos.Add(doc);
        await db.SaveChangesAsync();

        // La pregunta NO comparte palabras con "cambio de tarifa": prueba semántica.
        var query = await embeddings.EmbedAsync("¿puedo pasarme a un plan más barato?");
        var results = await new ChunkSearchService(db).SearchAsync(query, topK: 3);

        output.WriteLine("Pregunta: ¿puedo pasarme a un plan más barato?");
        foreach (var r in results)
            output.WriteLine($"  score={r.Score:F4}  {r.Content}");

        Assert.Contains("cambio de tarifa", results[0].Content);
    }
}
