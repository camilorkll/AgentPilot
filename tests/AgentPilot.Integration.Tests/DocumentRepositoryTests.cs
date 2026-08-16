using AgentPilot.Domain.Documents;
using AgentPilot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AgentPilot.Integration.Tests;

/// <summary>
/// Pruebas del repositorio de documentos contra Postgres real. Cubren el punto donde
/// una regla del dominio depende de cómo haya cargado EF la entidad: eso no se puede
/// comprobar con objetos construidos en memoria, porque ahí la colección siempre está
/// poblada.
/// </summary>
public class DocumentRepositoryTests(PgVectorFixture fixture) : IClassFixture<PgVectorFixture>
{
    private static readonly Guid Campaña = Guid.Parse("66666666-6666-6666-6666-666666666666");

    private static float[] Vector(int index)
    {
        var v = new float[AgentPilotDbContext.EmbeddingDimensions];
        v[index] = 1f;
        return v;
    }

    private async Task ResetAsync(AgentPilotDbContext db)
    {
        await db.Database.ExecuteSqlRawAsync("TRUNCATE campaigns, documents, chunks CASCADE;");
        await db.Database.ExecuteSqlInterpolatedAsync($@"
            INSERT INTO campaigns (""Id"", ""Name"", ""Status"", ""CreatedAtUtc"")
            VALUES ({Campaña}, 'Campaña de pruebas', 1, now());");
    }

    /// <summary>
    /// Regresión de un fallo real: el barrido de arranque que rescata los documentos
    /// atascados en Processing los localiza con ListAsync, que —a propósito— no carga los
    /// fragmentos, porque serían todos sus vectores. La regla de MarcarFallido miraba la
    /// colección en memoria, la veía vacía y marcaba como Failed documentos que sí tenían
    /// contenido indexado, sacándolos de las búsquedas: justo la pérdida de conocimiento
    /// que ese rescate venía a evitar. Con objetos construidos a mano no se reproduce,
    /// porque ahí la colección siempre viene llena.
    /// </summary>
    [Fact]
    public async Task UnDocumentoCargadoSinSusFragmentos_SigueSabiendoQueTieneContenido()
    {
        await using var db = fixture.CreateContext();
        await ResetAsync(db);

        var doc = new Documento(Campaña, "Escalado de incidencias", "escalado.md");
        doc.MarcarProcesando();
        doc.MarcarIndexado("test", [new Chunk(0, "nivel 2: 24 horas laborables", Vector(0))], "texto");
        db.Documentos.Add(doc);
        await db.SaveChangesAsync();

        // Se deja como lo dejaría un reinicio a media ingesta.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $@"UPDATE documents SET ""Status"" = 'Processing' WHERE ""Id"" = {doc.Id};");

        // Contexto nuevo: el de arriba tiene el documento cacheado CON sus chunks, así
        // que reutilizarlo escondería el fallo.
        await using var db2 = fixture.CreateContext();
        var repository = new DocumentRepository(db2);

        var interrumpidos = await repository.ListAsync(status: EstadoIngesta.Processing);
        var recuperado = Assert.Single(interrumpidos);
        Assert.Empty(recuperado.Chunks); // el listado no los trae...
        Assert.Equal(1, recuperado.ChunkCount); // ...pero la fila sabe que están.

        recuperado.MarcarFallido("La ingesta se interrumpió al reiniciarse la aplicación.");
        await repository.SaveChangesAsync();

        // Conserva contenido servible: vuelve a estar consultable, con el motivo anotado.
        Assert.Equal(EstadoIngesta.Ready, recuperado.Status);
        Assert.True(recuperado.ActualizacionFallidaConContenidoAnterior);

        // Y sus fragmentos siguen ahí: el rescate no tocó el índice.
        await using var db3 = fixture.CreateContext();
        var final = await new DocumentRepository(db3).GetByIdAsync(doc.Id);
        Assert.Single(final!.Chunks);
    }

    /// <summary>
    /// La otra cara: una primera ingesta interrumpida no tiene nada que preservar, así que
    /// se queda en Failed. Marcarla como Ready sería peor que el limbo — aparecería como
    /// indexada sin un solo fragmento.
    /// </summary>
    [Fact]
    public async Task UnaPrimeraIngestaInterrumpida_SeQuedaEnFallido()
    {
        await using var db = fixture.CreateContext();
        await ResetAsync(db);

        var doc = new Documento(Campaña, "Recién subido", "nuevo.md");
        doc.MarcarProcesando();
        db.Documentos.Add(doc);
        await db.SaveChangesAsync();

        await using var db2 = fixture.CreateContext();
        var repository = new DocumentRepository(db2);

        var interrumpido = Assert.Single(await repository.ListAsync(status: EstadoIngesta.Processing));
        Assert.Null(interrumpido.ChunkCount);

        interrumpido.MarcarFallido("La ingesta se interrumpió al reiniciarse la aplicación.");
        await repository.SaveChangesAsync();

        Assert.Equal(EstadoIngesta.Failed, interrumpido.Status);
        Assert.False(interrumpido.ActualizacionFallidaConContenidoAnterior);
    }
}
