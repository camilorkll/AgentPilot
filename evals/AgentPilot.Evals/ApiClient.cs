using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace AgentPilot.Evals;

/// <summary>
/// Medida de una pregunta. Se distinguen tres tiempos porque la experiencia del agente
/// depende de cuándo empieza a ver algo, no de cuándo termina la respuesta:
/// <list type="bullet">
///   <item><c>CitationsMs</c>: primer indicio en pantalla (las fuentes se emiten al recuperarlas).</item>
///   <item><c>FirstTokenMs</c>: el modelo empieza a redactar; con modelos de razonamiento es
///   donde se concentra la espera.</item>
///   <item><c>LatencyMs</c>: total que reporta el servidor.</item>
/// </list>
/// </summary>
public sealed record AskMeasurement(
    string Answer,
    string[] Documents,
    string Model,
    long LatencyMs,
    long CitationsMs,
    long FirstTokenMs,
    double CostUsd);

/// <summary>
/// Cliente HTTP del arnés: login, chat por SSE y las operaciones de administración de
/// campañas que necesita la comprobación de aislamiento (crear, cambiar de estado,
/// eliminar). La campaña es parámetro de cada pregunta y no del cliente: la comprobación
/// de fuga cruzada pregunta contra dos campañas distintas con la misma sesión.
/// </summary>
public class ApiClient(string baseUrl)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http = new() { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromMinutes(3) };

    public async Task LoginAsync(string username, string password)
    {
        var response = await _http.PostAsJsonAsync("/api/v1/auth/login", new { username, password });
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var token = payload.GetProperty("accessToken").GetString();
        _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    /// <summary>Lanza una pregunta contra una campaña y agrega el stream SSE en un único resultado.</summary>
    public async Task<AskMeasurement> AskAsync(string question, Guid campaignId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/chat/ask")
        {
            Content = new StringContent(
                JsonSerializer.Serialize(new { question, campaignId }),
                Encoding.UTF8, "application/json"),
        };

        // El cronómetro arranca antes de enviar: mide lo que espera el agente, no lo que tarda el modelo.
        var clock = Stopwatch.StartNew();
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var reader = new StreamReader(stream, Encoding.UTF8);

        var answer = new StringBuilder();
        var documents = new List<string>();
        var model = "desconocido";
        long latency = 0, citationsMs = 0, firstTokenMs = 0;
        double cost = 0;
        string? currentEvent = null;

        while (await reader.ReadLineAsync() is { } line)
        {
            if (line.StartsWith("event: ", StringComparison.Ordinal))
            {
                currentEvent = line[7..].Trim();
                continue;
            }
            if (!line.StartsWith("data: ", StringComparison.Ordinal)) continue;

            var data = line[6..];
            switch (currentEvent)
            {
                case "token":
                    if (firstTokenMs == 0) firstTokenMs = clock.ElapsedMilliseconds;
                    answer.Append(JsonSerializer.Deserialize<JsonElement>(data).GetProperty("text").GetString());
                    break;
                case "citations":
                    if (citationsMs == 0) citationsMs = clock.ElapsedMilliseconds;
                    foreach (var citation in JsonSerializer.Deserialize<JsonElement>(data).EnumerateArray())
                        documents.Add(citation.GetProperty("documentTitle").GetString() ?? "");
                    break;
                case "usage":
                    var usage = JsonSerializer.Deserialize<JsonElement>(data);
                    model = usage.GetProperty("model").GetString() ?? model;
                    latency = usage.GetProperty("latencyMs").GetInt64();
                    cost = usage.GetProperty("estimatedCostUsd").GetDouble();
                    break;
            }
        }

        return new AskMeasurement(
            answer.ToString(), documents.Distinct().ToArray(), model,
            latency, citationsMs, firstTokenMs, cost);
    }

    // --- Administración de campañas (solo para la comprobación de aislamiento) ---

    public async Task<Guid> CreateCampaignAsync(string name)
    {
        var response = await _http.PostAsJsonAsync("/api/v1/campaigns", new { name });
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("id").GetGuid();
    }

    public async Task SetCampaignStatusAsync(Guid campaignId, string status)
    {
        var response = await _http.PostAsJsonAsync(
            $"/api/v1/campaigns/{campaignId}/status", new { status });
        response.EnsureSuccessStatusCode();
    }

    /// <summary>Solo funciona si la campaña está cerrada; lo exige el propio dominio.</summary>
    public async Task DeleteCampaignAsync(Guid campaignId)
    {
        var response = await _http.DeleteAsync($"/api/v1/campaigns/{campaignId}");
        response.EnsureSuccessStatusCode();
    }
}
