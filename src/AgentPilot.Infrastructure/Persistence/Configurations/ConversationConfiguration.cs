using AgentPilot.Domain.Conversations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentPilot.Infrastructure.Persistence.Configurations;

public class ConversationConfiguration : IEntityTypeConfiguration<Conversation>
{
    public void Configure(EntityTypeBuilder<Conversation> builder)
    {
        builder.ToTable("conversations");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Title).HasMaxLength(200);
        builder.Property(c => c.CreatedAtUtc).IsRequired();

        // Operador que mantuvo la conversación. Indexado porque la revisión filtra por
        // él; anulable por el histórico anterior a registrarlo.
        builder.Property(c => c.UserName).HasMaxLength(100);
        builder.HasIndex(c => c.UserName);

        // Anulable por las conversaciones anteriores a las campañas. Al eliminar una
        // campaña la conversación NO se borra: no es corpus, es histórico, y perder
        // las preguntas ya atendidas dejaría los informes sin cuadrar.
        builder.HasOne<Domain.Campaigns.Campaña>()
            .WithMany()
            .HasForeignKey(c => c.CampaignId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(c => c.CampaignId);

        builder.HasMany(c => c.Messages)
            .WithOne()
            .HasForeignKey(m => m.ConversationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Metadata
            .FindNavigation(nameof(Conversation.Messages))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
