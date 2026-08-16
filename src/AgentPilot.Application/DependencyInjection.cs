using AgentPilot.Application.Abstractions;
using AgentPilot.Application.Auth;
using AgentPilot.Application.Campaigns;
using AgentPilot.Application.Chat;
using AgentPilot.Application.Feedback;
using AgentPilot.Application.Ingestion;
using Microsoft.Extensions.DependencyInjection;

namespace AgentPilot.Application;

/// <summary>Registro de servicios de la capa Application (lógica pura, casos de uso).</summary>
public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Chunker con tamaño 1000 / solapamiento 200 (valores por defecto).
        // Consciente de la estructura del Markdown (ADR-016): aísla tablas, corta por
        // encabezados y antepone la ruta. Cae a la ventana deslizante cuando no hay
        // estructura que respetar (un PDF extraído, prosa larga).
        services.AddSingleton<ITextChunker>(_ => new MarkdownAwareChunker());

        // Guarda de campaña: la usan todos los comandos que tocan documentación.
        services.AddScoped<CampaignGuard>();

        // Administración de campañas.
        services.AddScoped<ICampaignService, CampaignService>();

        // Orquestador de ingesta (scoped: usa el repositorio, que usa el DbContext).
        services.AddScoped<IDocumentIngestionService, DocumentIngestionService>();

        // Orquestador RAG de preguntas (scoped: usa el repositorio de conversaciones).
        services.AddScoped<IAskQuestionService, AskQuestionService>();

        // Vista previa de prompts: mismo orquestador RAG, sin persistir nada.
        services.AddScoped<IPromptPreviewService, PromptPreviewService>();

        // Autenticación.
        services.AddScoped<IAuthService, AuthService>();

        // Feedback.
        services.AddScoped<IFeedbackService, FeedbackService>();

        return services;
    }
}
