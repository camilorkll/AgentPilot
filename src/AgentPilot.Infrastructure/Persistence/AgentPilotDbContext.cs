using AgentPilot.Domain.Documents;
using Microsoft.EntityFrameworkCore;

namespace AgentPilot.Infrastructure.Persistence;

/// <summary>
/// Sesión con la base de datos. Vive en Infrastructure: es un detalle técnico
/// del que el dominio no sabe nada. Traduce las entidades a tablas de PostgreSQL.
/// </summary>
public class AgentPilotDbContext(DbContextOptions<AgentPilotDbContext> options)
    : DbContext(options)
{
    /// <summary>
    /// Dimensiones del vector de embedding. text-embedding-3-small de OpenAI
    /// produce 1536; nomic-embed-text de Ollama produce 768. La columna vector
    /// se declara con este tamaño fijo: cambiar de proveedor implica cambiar
    /// esta constante y recrear el esquema (ver ADR-005).
    /// </summary>
    public const int EmbeddingDimensions = 1536;

    public DbSet<Documento> Documentos => Set<Documento>();
    public DbSet<Chunk> Chunks => Set<Chunk>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Habilita la extensión pgvector: la migración incluirá
        // "CREATE EXTENSION IF NOT EXISTS vector".
        modelBuilder.HasPostgresExtension("vector");

        // Carga todas las clases IEntityTypeConfiguration de este ensamblado.
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AgentPilotDbContext).Assembly);
    }
}
