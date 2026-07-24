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

        // Índice por fecha: las consultas del dashboard filtran/agrupan por fecha.
        builder.HasIndex(l => l.CreatedAtUtc);
    }
}
