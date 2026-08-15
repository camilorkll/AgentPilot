using AgentPilot.Domain.Conversations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentPilot.Infrastructure.Persistence.Configurations;

public class FeedbackConfiguration : IEntityTypeConfiguration<Feedback>
{
    public void Configure(EntityTypeBuilder<Feedback> builder)
    {
        builder.ToTable("feedback");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();

        builder.Property(f => f.Rating).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(f => f.Comment).HasMaxLength(1000);
        builder.Property(f => f.CreatedBy).HasMaxLength(100);
        builder.Property(f => f.CreatedAtUtc).IsRequired();

        // Clave foránea al mensaje valorado; si el mensaje se borra, el feedback también.
        builder.HasOne<Message>()
            .WithMany()
            .HasForeignKey(f => f.MessageId)
            .OnDelete(DeleteBehavior.Cascade);

        // Único: una respuesta se valora una vez y se puede rectificar. Dos filas para
        // el mismo mensaje contarían esa respuesta dos veces en el porcentaje de
        // respuestas útiles. El upsert de FeedbackService respeta la regla; este índice
        // la garantiza aunque alguien escriba en la tabla por otro camino.
        builder.HasIndex(f => f.MessageId).IsUnique();
    }
}
