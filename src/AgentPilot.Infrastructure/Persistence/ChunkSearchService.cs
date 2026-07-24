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
        float[] queryEmbedding, int topK = 5, CancellationToken cancellationToken = default)
    {
        // pgvector espera el vector como "[f1,f2,...]"; lo casteamos a 'vector'.
        var vectorLiteral =
            "[" + string.Join(",", queryEmbedding.Select(f => f.ToString(CultureInfo.InvariantCulture))) + "]";

        // <=> es la distancia coseno (0 = idéntico). Similitud = 1 - distancia.
        FormattableString sql = $@"
            SELECT c.""Id""          AS ""ChunkId"",
                   c.""DocumentId""  AS ""DocumentId"",
                   d.""Title""       AS ""DocumentTitle"",
                   c.""Ordinal""     AS ""Ordinal"",
                   c.""Content""     AS ""Content"",
                   1 - (c.""Embedding"" <=> CAST({vectorLiteral} AS vector)) AS ""Score""
            FROM chunks c
            JOIN documents d ON d.""Id"" = c.""DocumentId""
            WHERE d.""Status"" = 'Ready'
            ORDER BY c.""Embedding"" <=> CAST({vectorLiteral} AS vector)
            LIMIT {topK}";

        return await db.Database
            .SqlQuery<ChunkMatch>(sql)
            .ToListAsync(cancellationToken);
    }
}
