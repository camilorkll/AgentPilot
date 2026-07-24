using AgentPilot.Domain.Conversations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentPilot.Infrastructure.Persistence.Configurations;

public class MessageConfiguration : IEntityTypeConfiguration<Message>
{
    public void Configure(EntityTypeBuilder<Message> builder)
    {
        builder.ToTable("messages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.Role).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(m => m.Content).IsRequired();
        builder.Property(m => m.CreatedAtUtc).IsRequired();
        builder.Property(m => m.ConversationId).IsRequired();

        // Las citas se guardan como una columna JSON (jsonb) dentro del mensaje:
        // siempre se leen con su mensaje y nunca se consultan por separado.
        builder.OwnsMany(m => m.Citations, citations =>
        {
            citations.ToJson();
            citations.Property(c => c.DocumentTitle);
            citations.Property(c => c.Snippet);
        });

        builder.Navigation(m => m.Citations)
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
