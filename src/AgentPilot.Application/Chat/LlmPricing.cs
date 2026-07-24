namespace AgentPilot.Application.Chat;

/// <summary>
/// Estimación de coste por tokens. Precios aproximados en USD por millón de
/// tokens (entrada, salida); ajustar si cambian las tarifas del proveedor.
/// </summary>
public static class LlmPricing
{
    private static readonly Dictionary<string, (double Input, double Output)> Rates =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["gpt-5"] = (1.25, 10.00),
            ["gpt-5-mini"] = (0.25, 2.00),
            ["gpt-4o-mini"] = (0.15, 0.60),
        };

    public static double EstimateUsd(string model, int promptTokens, int completionTokens)
    {
        var (input, output) = Rates.TryGetValue(model, out var rate) ? rate : (0d, 0d);
        return promptTokens / 1_000_000d * input + completionTokens / 1_000_000d * output;
    }
}
