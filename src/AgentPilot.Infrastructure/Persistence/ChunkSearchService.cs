using System.Globalization;
using AgentPilot.Application.Abstractions;
using AgentPilot.Application.Retrieval;
using Microsoft.EntityFrameworkCore;

namespace AgentPilot.Infrastructure.Persistence;

/// <summary>
/// Búsqueda por similitud con pgvector. Usa SQL crudo porque el operador de
/// distancia coseno (&lt;=&gt;) es nativo de pgvector y no se traduce desde LINQ
/// cuando el embedding está mapeado con un converter float[] &lt;-&gt; Vector.
/// </summary>
public class ChunkSearchService(AgentPilotDbContext db) : IChunkSearchService
{
    public async Task<IReadOnlyList<ChunkMatch>> SearchAsync(
        float[] queryEmbedding, Guid campaignId, int topK = 5,
        CancellationToken cancellationToken = default)
    {
        if (campaignId == Guid.Empty)
            throw new ArgumentException(
                "La búsqueda exige una campaña: sin ella recuperaría documentación de " +
                "cualquier campaña.", nameof(campaignId));

        // pgvector espera el vector como "[f1,f2,...]"; lo casteamos a 'vector'.
        var vectorLiteral =
            "[" + string.Join(",", queryEmbedding.Select(f => f.ToString(CultureInfo.InvariantCulture))) + "]";

        // <=> es la distancia coseno (0 = idéntico). Similitud = 1 - distancia.
        // La similitud se calcula una vez en la subconsulta y se proyecta dos veces fuera:
        // ""Score"" es lo que mide, y ""Relevance"" arranca valiendo lo mismo porque aquí
        // todavía no ha reordenado nadie (el reordenado la sustituye después por su propia
        // puntuación). Repetir la expresión en el SELECT costaría una operación vectorial
        // más por fila, en el camino que el agente espera durante la llamada.
        FormattableString sql = $@"
            SELECT t.""ChunkId"",
                   t.""DocumentId"",
                   t.""DocumentTitle"",
                   t.""Ordinal"",
                   t.""Content"",
                   t.""Similitud"" AS ""Score"",
                   t.""Similitud"" AS ""Relevance""
            FROM (
                SELECT c.""Id""          AS ""ChunkId"",
                       c.""DocumentId""  AS ""DocumentId"",
                       d.""Title""       AS ""DocumentTitle"",
                       c.""Ordinal""     AS ""Ordinal"",
                       c.""Content""     AS ""Content"",
                       1 - (c.""Embedding"" <=> CAST({vectorLiteral} AS vector)) AS ""Similitud""
                FROM chunks c
                JOIN documents d ON d.""Id"" = c.""DocumentId""
                WHERE d.""CampaignId"" = {campaignId}
                  AND d.""Status"" = 'Ready'
                  AND d.""IsActive"" = true
                ORDER BY c.""Embedding"" <=> CAST({vectorLiteral} AS vector)
                LIMIT {topK}
            ) t
            ORDER BY t.""Similitud"" DESC";

        return await db.Database
            .SqlQuery<ChunkMatch>(sql)
            .ToListAsync(cancellationToken);
    }
}
