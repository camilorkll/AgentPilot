using AgentPilot.Domain.Campaigns;

namespace AgentPilot.Application.Campaigns;

/// <summary>
/// Resultado de publicar unas instrucciones de campaña (ya sea editándolas o
/// restaurando una versión pasada): lo aplicado, los avisos de lint no bloqueantes
/// y la entrada de historial que se acaba de crear.
/// </summary>
public record PromptUpdateResult(
    AssistantPromptSettings Settings,
    IReadOnlyList<string> Warnings,
    Guid VersionId,
    DateTime CreatedAtUtc);
