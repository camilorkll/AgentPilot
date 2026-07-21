namespace AgentPilot.Application.Abstractions;

/// <summary>Trocea un texto largo en fragmentos aptos para vectorizar.</summary>
public interface ITextChunker
{
    IReadOnlyList<string> Split(string text);
}
