using AgentPilot.Domain.Campaigns;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentPilot.Infrastructure.Persistence.Configurations;

/// <summary>Mapeo de Campaña a la tabla 'campaigns'.</summary>
public class CampañaConfiguration : IEntityTypeConfiguration<Campaña>
{
    public void Configure(EntityTypeBuilder<Campaña> builder)
    {
        builder.ToTable("campaigns");
        builder.HasKey(c => c.Id);
        // El Id lo genera el dominio, no la BD (coherencia con Documento y Chunk).
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Name).IsRequired().HasMaxLength(Campaña.MaxLongitudNombre);

        // A diferencia de EstadoIngesta, que se guarda como texto, aquí el estado se
        // persiste como entero porque así se especificó la tabla (0/1/2). El contrato
        // OpenAPI lo expone con nombre para que la API siga siendo legible.
        builder.Property(c => c.Status).HasConversion<int>().IsRequired();

        builder.Property(c => c.AssistantInstructions)
            .HasMaxLength(Campaña.MaxLongitudInstrucciones);

        builder.Property(c => c.ClosedAtUtc);
        builder.Property(c => c.CreatedAtUtc).IsRequired();

        // El índice único va sobre lower("Name") y se crea con SQL en la migración:
        // es un índice funcional y el API fluido de EF no puede expresarlo. Sin él,
        // "Luz y Gas" y "luz y gas" serían dos campañas distintas, y quien las viera
        // en el selector no sabría en cuál está la documentación buena.
    }
}
