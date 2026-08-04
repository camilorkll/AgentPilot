using System.Text.RegularExpressions;

namespace AgentPilot.Application.Metrics;

/// <summary>
/// Rango de meses ya resuelto a instantes UTC concretos, más los meses (YYYY-MM) que
/// el informe debe devolver para que el cliente sepa qué se aplicó realmente cuando
/// hubo valores por defecto de por medio.
/// </summary>
/// <param name="ToUtcExclusive">
/// Límite superior EXCLUSIVO: el instante UTC del primer segundo del mes siguiente a
/// <paramref name="MonthTo"/> en hora de Europe/Madrid. Exclusivo y no "23:59:59" para
/// no depender de dónde cae el límite de milisegundo.
/// </param>
public sealed record MonthRange(string MonthFrom, string MonthTo, DateTime FromUtc, DateTime ToUtcExclusive);

public static class Month
{
    private static readonly Regex Pattern = new(@"^\d{4}-(0[1-9]|1[0-2])$", RegexOptions.Compiled);

    /// <summary>Lanza <see cref="FormatException"/> si no tiene forma YYYY-MM.</summary>
    public static void Validate(string? value, string paramName)
    {
        if (value is not null && !Pattern.IsMatch(value))
            throw new FormatException($"{paramName} debe tener forma YYYY-MM (recibido: '{value}').");
    }

    /// <summary>
    /// True si ambos meses vienen informados y el final es anterior al inicial. La
    /// comparación es de texto porque "YYYY-MM" ordena igual lexicográfica que
    /// cronológicamente, y evita tener que parsear nada para esta validación.
    /// </summary>
    public static bool IsInvertedRange(string? monthFrom, string? monthTo) =>
        monthFrom is not null && monthTo is not null &&
        string.CompareOrdinal(monthTo, monthFrom) < 0;
}
