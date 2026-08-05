using System.Net;
using System.Text;
using AgentPilot.Application.Chat;
using AgentPilot.Infrastructure.Ai;
using AgentPilot.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace AgentPilot.Integration.Tests;

/// <summary>
/// OllamaChatCompletionService habla la API nativa de Ollama (streaming NDJSON), no la
/// compatible con OpenAI, precisamente para poder fijar <c>num_ctx</c> explícitamente
/// (§7.3 del plan): sin eso, Ollama trunca el contexto en silencio y el síntoma —el
/// asistente se abstiene o responde mal con las fuentes bien recuperadas— no señala
/// dónde está el problema. Estas pruebas verifican esa composición del payload y el
/// parseo del stream con un HttpMessageHandler falso, sin depender de un servidor
/// Ollama real. La última prueba sí habla con un Ollama real si lo encuentra en
/// localhost:11434 (se omite si no, igual que ChatCompletionTests con OpenAI).
/// </summary>
public class OllamaChatCompletionTests
{
    [Fact]
    public async Task StreamAsync_EnviaModeloYNumCtx_YParseaElStreamNdjson()
    {
        var ndjson = string.Join("\n",
            """{"message":{"content":"Hola"},"done":false}""",
            """{"message":{"content":" mundo"},"done":false}""",
            """{"message":{"content":""},"done":true,"prompt_eval_count":42,"eval_count":7}""")
            + "\n";

        HttpRequestMessage? peticiónCapturada = null;
        string? cuerpoCapturado = null;

        var handler = new FakeHandler(async request =>
        {
            peticiónCapturada = request;
            cuerpoCapturado = await request.Content!.ReadAsStringAsync();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(ndjson, Encoding.UTF8, "application/x-ndjson"),
            };
        });

        var service = new OllamaChatCompletionService(
            new HttpClient(handler),
            Options.Create(new ChatOptions
            {
                OllamaBaseUrl = "http://localhost:11434",
                OllamaModel = "llama3.2:3b",
                OllamaNumCtx = 4096,
            }));

        var messages = new List<PromptMessage>
        {
            new(PromptRole.System, "Eres un asistente."),
            new(PromptRole.User, "Hola"),
        };

        var deltas = new List<string>();
        ChatUsage? usage = null;
        await foreach (var chunk in service.StreamAsync(messages))
        {
            if (chunk.TextDelta is not null) deltas.Add(chunk.TextDelta);
            if (chunk.Usage is not null) usage = chunk.Usage;
        }

        Assert.Equal("Hola mundo", string.Concat(deltas));
        Assert.NotNull(usage);
        Assert.Equal(42, usage!.PromptTokens);
        Assert.Equal(7, usage.CompletionTokens);

        Assert.Equal("llama3.2:3b", service.ModelName);
        Assert.EndsWith("/api/chat", peticiónCapturada!.RequestUri!.AbsolutePath);
        // num_ctx fijado explícitamente: es la corrección del problema real del plan
        // (Ollama por defecto usa 2048 y trunca el prompt de AgentPilot en silencio).
        Assert.Contains("\"num_ctx\":4096", cuerpoCapturado);
        Assert.Contains("\"model\":\"llama3.2:3b\"", cuerpoCapturado);
        Assert.Contains("\"role\":\"system\"", cuerpoCapturado);
        Assert.Contains("\"stream\":true", cuerpoCapturado);
    }

    [Fact]
    public async Task StreamAsync_ConRespuestaDeErrorHttp_LanzaExcepcion()
    {
        var handler = new FakeHandler(
            _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError)));
        var service = new OllamaChatCompletionService(new HttpClient(handler), Options.Create(new ChatOptions()));

        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await foreach (var _ in service.StreamAsync([new PromptMessage(PromptRole.User, "hola")])) { }
        });
    }

    /// <summary>
    /// Contra un Ollama real si está accesible en local; se omite si no (igual que
    /// ChatCompletionTests se omite sin OPENAI_API_KEY). Sirve para verificar en el
    /// momento en que Ollama esté instalado, sin tener que escribir esta prueba de nuevo.
    /// </summary>
    [SkippableFact]
    public async Task StreamAsync_ContraOllamaReal_RespondeEnStreaming()
    {
        // 127.0.0.1 y no "localhost": ver el porqué en ChatOptions.OllamaBaseUrl.
        var baseUrl = Environment.GetEnvironmentVariable("CHAT_OLLAMA_BASE_URL") ?? "http://127.0.0.1:11434";
        Skip.IfNot(await EstáOllamaDisponibleAsync(baseUrl),
            $"Ollama no está accesible en {baseUrl}: se omite el test contra el servidor real.");

        var model = Environment.GetEnvironmentVariable("CHAT_OLLAMA_MODEL") ?? "llama3.2:3b";
        var service = new OllamaChatCompletionService(
            new HttpClient(),
            Options.Create(new ChatOptions { OllamaBaseUrl = baseUrl, OllamaModel = model, OllamaNumCtx = 4096 }));

        var messages = new List<PromptMessage>
        {
            new(PromptRole.System, "Responde en español, en una sola frase breve."),
            new(PromptRole.User, "¿Cuál es la capital de Francia?"),
        };

        var respuesta = new StringBuilder();
        var deltas = 0;
        ChatUsage? usage = null;

        await foreach (var chunk in service.StreamAsync(messages))
        {
            if (chunk.TextDelta is not null) { respuesta.Append(chunk.TextDelta); deltas++; }
            if (chunk.Usage is not null) usage = chunk.Usage;
        }

        Assert.True(deltas > 0, "El streaming no emitió ningún token.");
        Assert.NotNull(usage);
    }

    private static async Task<bool> EstáOllamaDisponibleAsync(string baseUrl)
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
            var response = await http.GetAsync($"{baseUrl}/api/tags");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => respond(request);
    }
}
