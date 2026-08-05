using System.Text;
using AgentPilot.Application.Retrieval;

namespace AgentPilot.Application.Chat;

/// <summary>
/// Construye el bloque &lt;contexto&gt; con los fragmentos recuperados, que acompaña a
/// la pregunta del agente en el mensaje de usuario. Lo usan tanto el chat real
/// (<see cref="AskQuestionService"/>) como la vista previa de prompts
/// (<see cref="PromptPreviewService"/>), para que ambos vean literalmente el mismo
/// contexto recuperado y no dos formatos distintos.
/// </summary>
public static class ContextBlockBuilder
{
    public static string Build(IReadOnlyList<ChunkMatch> matches)
    {
        var sb = new StringBuilder();
        sb.AppendLine("<contexto>");
        if (matches.Count == 0)
        {
            sb.AppendLine("(No se encontraron fragmentos relevantes en la base de conocimiento.)");
        }
        else
        {
            for (int i = 0; i < matches.Count; i++)
                sb.AppendLine($"[{i + 1}] (Documento: \"{matches[i].DocumentTitle}\") {matches[i].Content}");
        }
        sb.Append("</contexto>");
        return sb.ToString();
    }
}
