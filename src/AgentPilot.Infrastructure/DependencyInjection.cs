using AgentPilot.Application.Abstractions;
using AgentPilot.Application.Auth;
using AgentPilot.Infrastructure.Ai;
using AgentPilot.Infrastructure.Auth;
using AgentPilot.Infrastructure.Configuration;
using AgentPilot.Infrastructure.Ingestion;
using AgentPilot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentPilot.Infrastructure;

/// <summary>
/// Punto de entrada de la capa Infrastructure: registra sus servicios en el
/// contenedor de DI. La API llama a AddInfrastructure(...) y no necesita saber
/// qué hay dentro (DbContext, proveedores de IA, etc.).
/// </summary>
public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services, IConfiguration configuration)
    {
        // --- Persistencia ---
        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "Falta la cadena de conexión 'ConnectionStrings:Default'.");

        services.AddDbContext<AgentPilotDbContext>(options =>
            // UseVector() activa el mapeo del tipo pgvector en Npgsql.
            options.UseNpgsql(connectionString, npgsql => npgsql.UseVector()));

        // --- IA: embeddings ---
        services.Configure<OpenAiOptions>(configuration.GetSection(OpenAiOptions.SectionName));
        services.Configure<EmbeddingsOptions>(configuration.GetSection(EmbeddingsOptions.SectionName));

        // El proveedor se elige por configuración (Embeddings:Provider). Ambas
        // implementaciones cumplen IEmbeddingService; el resto de la app es ajena
        // a cuál se registró.
        var provider = configuration[$"{EmbeddingsOptions.SectionName}:Provider"] ?? "openai";
        if (string.Equals(provider, "ollama", StringComparison.OrdinalIgnoreCase))
            services.AddHttpClient<IEmbeddingService, OllamaEmbeddingService>();
        else
            services.AddSingleton<IEmbeddingService, OpenAiEmbeddingService>();

        // --- Extracción de texto de documentos (PDF / Markdown) ---
        services.AddSingleton<IDocumentTextExtractor, DocumentTextExtractor>();

        // --- Ingesta: repositorio, cola en memoria y worker en segundo plano ---
        services.AddScoped<IDocumentRepository, DocumentRepository>();
        services.AddSingleton<IIngestionQueue, InMemoryIngestionQueue>();
        services.AddHostedService<IngestionBackgroundService>();

        // --- Recuperación: búsqueda por similitud ---
        services.AddScoped<IChunkSearchService, ChunkSearchService>();

        // --- Métricas / observabilidad ---
        services.AddScoped<IMetricsRepository, MetricsRepository>();

        // --- Conversaciones y feedback ---
        services.AddScoped<IConversationRepository, ConversationRepository>();
        services.AddScoped<IFeedbackRepository, FeedbackRepository>();

        // --- Autenticación (usuarios, hashing, tokens) ---
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();
        services.AddSingleton<IJwtTokenGenerator, JwtTokenGenerator>();

        // --- IA: chat (generación de respuestas) ---
        services.AddSingleton<IChatCompletionService, OpenAiChatCompletionService>();

        return services;
    }
}
