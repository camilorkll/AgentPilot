using AgentPilot.Domain.Campaigns;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentPilot.Infrastructure.Persistence.Configurations;

/// <summary>Mapeo de PromptVersion a la tabla 'prompt_versions'.</summary>
public class PromptVersionConfiguration : IEntityTypeConfiguration<PromptVersion>
{
    public void Configure(EntityTypeBuilder<PromptVersion> builder)
    {
        builder.ToTable("prompt_versions");
        builder.HasKey(v => v.Id);
        builder.Property(v => v.Id).ValueGeneratedNever();

        builder.Property(v => v.CampaignId).IsRequired();
        builder.Property(v => v.SettingsJson).IsRequired().HasColumnType("jsonb");
        builder.Property(v => v.PublishedBy).IsRequired().HasMaxLength(100);
        builder.Property(v => v.CreatedAtUtc).IsRequired();

        // El historial de una campaña se lista siempre en orden cronológico y nunca
        // sin filtrar por campaña: es el acceso que importa indexar.
        builder.HasIndex(v => new { v.CampaignId, v.CreatedAtUtc });

        // Sin propiedad de navegación en Campaña a propósito (igual que Documento):
        // es un registro de auditoría que se consulta por su cuenta, no algo que haya
        // que cargar junto con la campaña. Solo la clave foránea, para que borrar una
        // campaña se lleve también su historial de prompts.
        builder.HasOne<Campaña>()
            .WithMany()
            .HasForeignKey(v => v.CampaignId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
