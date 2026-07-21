using AgentPilot.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Pgvector;

namespace AgentPilot.Infrastructure.Persistence.Configurations;

/// <summary>Mapeo de Chunk a la tabla 'chunks'.</summary>
public class ChunkConfiguration : IEntityTypeConfiguration<Chunk>
{
    public void Configure(EntityTypeBuilder<Chunk> builder)
    {
        builder.ToTable("chunks");
        builder.HasKey(c => c.Id);
        // El Id lo genera el dominio (Guid.NewGuid en el constructor), no la BD.
        // Sin esto, EF trata los chunks nuevos con Id ya puesto como existentes y
        // emite UPDATE en vez de INSERT.
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Ordinal).IsRequired();
        builder.Property(c => c.Content).IsRequired();
        builder.Property(c => c.DocumentId).IsRequired();

        // (1) EL PUENTE dominio ↔ infraestructura.
        // En el dominio el embedding es float[] (limpio, sin pgvector). Aquí lo
        // convertimos al tipo Vector de pgvector al guardar, y de vuelta a
        // float[] al leer. El dominio nunca ve el tipo Vector.
        builder.Property(c => c.Embedding)
            .HasColumnType($"vector({AgentPilotDbContext.EmbeddingDimensions})")
            .HasConversion(
                dominio => new Vector(dominio),   // float[] -> Vector (escritura)
                bd => bd.ToArray(),               // Vector -> float[] (lectura)
                // Comparador para que EF detecte cambios en el array elemento a elemento.
                new ValueComparer<float[]>(
                    (a, b) => a!.SequenceEqual(b!),
                    v => v.Aggregate(0, (hash, f) => HashCode.Combine(hash, f.GetHashCode())),
                    v => v.ToArray()));

        // (2) Índice HNSW para búsqueda por similitud coseno. En un corpus grande
        // acelera muchísimo el "buscar los N chunks más parecidos". Para el corpus
        // del MVP no es imprescindible, pero demuestra madurez de indexado.
        builder.HasIndex(c => c.Embedding)
            .HasMethod("hnsw")
            .HasOperators("vector_cosine_ops");
    }
}
