using AgentPilot.Domain.Telemetry;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentPilot.Infrastructure.Persistence.Configurations;

public class LlmCallLogConfiguration : IEntityTypeConfiguration<LlmCallLog>
{
    public void Configure(EntityTypeBuilder<LlmCallLog> builder)
    {
        builder.ToTable("llm_call_logs");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();

        builder.Property(l => l.Model).IsRequired().HasMaxLength(100);
        builder.Property(l => l.CreatedAtUtc).IsRequired();

        // Sin clave foránea a campaigns a propósito: el nombre va desnormalizado para
        // que el informe de coste por campaña siga leyéndose después de eliminarla.
        builder.Property(l => l.CampaignName).HasMaxLength(Domain.Campaigns.Campaña.MaxLongitudNombre);

        // Índice por fecha: las consultas del dashboard filtran/agrupan por fecha.
        builder.HasIndex(l => l.CreatedAtUtc);
        builder.HasIndex(l => l.CampaignId);
    }
}
