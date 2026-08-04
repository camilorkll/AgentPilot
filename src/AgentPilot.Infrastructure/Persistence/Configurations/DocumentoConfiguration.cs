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

        builder.Property(d => d.CampaignId).IsRequired();
        builder.Property(d => d.Title).IsRequired().HasMaxLength(300);
        builder.Property(d => d.FileName).IsRequired().HasMaxLength(300);

        // El enum se guarda como texto ("Ready") en vez de como número (2):
        // la tabla es legible y resistente a reordenar el enum.
        builder.Property(d => d.Status).HasConversion<string>().HasMaxLength(20).IsRequired();

        builder.Property(d => d.EmbeddingModel).HasMaxLength(100);
        builder.Property(d => d.ErrorMessage);
        builder.Property(d => d.CreatedAtUtc).IsRequired();

        // Los documentos existentes siguen activos tras la migración.
        builder.Property(d => d.IsActive).IsRequired().HasDefaultValue(true);

        // La campaña se referencia por clave foránea sin navegación: son agregados
        // distintos y Documento no debe arrastrar la campaña al cargarse. En cascada
        // porque eliminar una campaña se lleva su corpus; que solo se pueda eliminar
        // estando cerrada es una regla de negocio y vive en el dominio, no aquí: la
        // base de datos protege de estados imposibles, el dominio de decisiones
        // indebidas.
        builder.HasOne<Domain.Campaigns.Campaña>()
            .WithMany()
            .HasForeignKey(d => d.CampaignId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(d => d.CampaignId);

        // Un mismo fichero no puede repetirse dentro de una campaña (duplicaría sus
        // fragmentos y ensuciaría las citas), pero sí puede existir en otra: son
        // corpus independientes.
        builder.HasIndex(d => new { d.CampaignId, d.FileName }).IsUnique();

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
