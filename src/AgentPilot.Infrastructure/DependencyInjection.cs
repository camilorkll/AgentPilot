using AgentPilot.Application.Abstractions;
using AgentPilot.Infrastructure.Ai;
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

        return services;
    }
}
