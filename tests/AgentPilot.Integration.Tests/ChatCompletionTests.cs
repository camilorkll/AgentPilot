using System.Text;
using AgentPilot.Application.Chat;
using AgentPilot.Infrastructure.Ai;
using AgentPilot.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;

namespace AgentPilot.Integration.Tests;

/// <summary>
/// Verifica que el chat de OpenAI responde en streaming. Llama a la API real;
/// se omite si no hay OPENAI_API_KEY.
/// </summary>
public class ChatCompletionTests(ITestOutputHelper output)
{
    private static string? ApiKey => Environment.GetEnvironmentVariable("OPENAI_API_KEY");
    private static string ChatModel =>
        Environment.GetEnvironmentVariable("OPENAI_CHAT_MODEL") ?? "gpt-5-mini";

    [SkippableFact]
    public async Task Chat_RespondeEnStreaming()
    {
        Skip.If(string.IsNullOrWhiteSpace(ApiKey),
            "Sin OPENAI_API_KEY: se omite el test de chat real.");

        var chat = new OpenAiChatCompletionService(Options.Create(
            new OpenAiOptions { ApiKey = ApiKey, ChatModel = ChatModel }));

        var messages = new List<PromptMessage>
        {
            new(PromptRole.System, "Responde en español, en una sola frase breve."),
            new(PromptRole.User, "¿Cuál es la capital de Francia?"),
        };

        var respuesta = new StringBuilder();
        int deltas = 0;
        ChatUsage? usage = null;

        await foreach (var chunk in chat.StreamAsync(messages))
        {
            if (chunk.TextDelta is not null) { respuesta.Append(chunk.TextDelta); deltas++; }
            if (chunk.Usage is not null) usage = chunk.Usage;
        }

        output.WriteLine($"Modelo: {ChatModel}");
        output.WriteLine($"Deltas recibidos: {deltas}");
        output.WriteLine($"Respuesta: {respuesta}");
        if (usage is not null)
            output.WriteLine($"Tokens: entrada={usage.PromptTokens}, salida={usage.CompletionTokens}");

        Assert.True(deltas > 0, "El streaming no emitió ningún token.");
        Assert.Contains("Par", respuesta.ToString(), StringComparison.OrdinalIgnoreCase); // París
    }
}
