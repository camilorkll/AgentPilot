using AgentPilot.Infrastructure.Ai;
using AgentPilot.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Xunit.Abstractions;

namespace AgentPilot.Integration.Tests;

/// <summary>
/// Demuestra que los embeddings capturan proximidad semántica: textos con
/// significado parecido producen vectores más cercanos (mayor similitud coseno).
/// Llama a la API real de OpenAI; se omite si no hay OPENAI_API_KEY en el entorno.
/// </summary>
public class EmbeddingSemanticTests(ITestOutputHelper output)
{
    private static string? ApiKey => Environment.GetEnvironmentVariable("OPENAI_API_KEY");

    [SkippableFact]
    public async Task Embeddings_ColocanCercaLoSemanticamenteParecido()
    {
        Skip.If(string.IsNullOrWhiteSpace(ApiKey),
            "Sin OPENAI_API_KEY en el entorno: se omite el test que llama a la API real.");

        var service = new OpenAiEmbeddingService(Options.Create(
            new OpenAiOptions { ApiKey = ApiKey, EmbeddingModel = "text-embedding-3-small" }));

        var vectores = await service.EmbedBatchAsync(["gato", "felino", "excavadora"]);
        float[] gato = vectores[0], felino = vectores[1], excavadora = vectores[2];

        double simFelino = SimilitudCoseno(gato, felino);
        double simExcavadora = SimilitudCoseno(gato, excavadora);

        output.WriteLine($"Dimensiones del vector: {gato.Length}");
        output.WriteLine($"similitud(gato, felino)     = {simFelino:F4}");
        output.WriteLine($"similitud(gato, excavadora) = {simExcavadora:F4}");

        Assert.Equal(1536, gato.Length);
        Assert.True(simFelino > simExcavadora,
            $"Se esperaba que 'gato'~'felino' ({simFelino:F4}) fuera mayor que " +
            $"'gato'~'excavadora' ({simExcavadora:F4}).");
    }

    // Similitud coseno: 1 = idénticos en dirección, 0 = sin relación, -1 = opuestos.
    private static double SimilitudCoseno(float[] a, float[] b)
    {
        double dot = 0, normA = 0, normB = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }
        return dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}
