using AgentPilot.Domain.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentPilot.Infrastructure.Persistence.Configurations;

/// <summary>Mapeo de Documento a la tabla 'documents'.</summary>
public class DocumentoConfiguration : IEntityTypeConfiguration<Documento>
{
    public void Configure(EntityTypeBuilder<Documento> builder)
    {
        builder.ToTable("documents");
        builder.HasKey(d => d.Id);
        // El Id lo genera el dominio, no la BD (coherencia con Chunk).
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.Title).IsRequired().HasMaxLength(300);
        builder.Property(d => d.FileName).IsRequired().HasMaxLength(300);

        // El enum se guarda como texto ("Ready") en vez de como número (2):
        // la tabla es legible y resistente a reordenar el enum.
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(d => d.EmbeddingModel).HasMaxLength(100);
        builder.Property(d => d.ErrorMessage);
        builder.Property(d => d.CreatedAtUtc).IsRequired();

        // Relación 1-a-N: un documento tiene muchos chunks. Al borrar el
        // documento, se borran sus chunks en cascada.
        builder.HasMany(d => d.Chunks)
            .WithOne()
            .HasForeignKey(c => c.DocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        // La colección se expone como IReadOnlyCollection respaldada por el campo
        // privado _chunks. Le decimos a EF que use ese campo, no la propiedad.
        builder.Metadata
            .FindNavigation(nameof(Documento.Chunks))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
