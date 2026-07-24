using AgentPilot.Application.Abstractions;
using AgentPilot.Application.Auth;
using AgentPilot.Application.Chat;
using AgentPilot.Application.Ingestion;
using Microsoft.Extensions.DependencyInjection;

namespace AgentPilot.Application;

/// <summary>Registro de servicios de la capa Application (lógica pura, casos de uso).</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Chunker con tamaño 1000 / solapamiento 200 (valores por defecto).
        services.AddSingleton<ITextChunker>(_ => new SlidingWindowChunker());

        // Orquestador de ingesta (scoped: usa el repositorio, que usa el DbContext).
        services.AddScoped<IDocumentIngestionService, DocumentIngestionService>();

        // Orquestador RAG de preguntas (scoped: usa el repositorio de conversaciones).
        services.AddScoped<IAskQuestionService, AskQuestionService>();

        // Autenticación.
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}
