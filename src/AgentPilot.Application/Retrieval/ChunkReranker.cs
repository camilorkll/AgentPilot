using System.Globalization;
using System.Text;

namespace AgentPilot.Application.Retrieval;

/// <summary>
/// Reordena los fragmentos recuperados combinando la similitud vectorial con el solape
/// léxico respecto a la pregunta, y se queda con los mejores.
///
/// El porqué, medido: la búsqueda vectorial acierta el tema pero no distingue bien
/// dentro de él. Ante "¿se acumulan los datos no consumidos?", varios fragmentos del
/// catálogo de tarifas puntúan parecido, y el que contiene literalmente "los datos no
/// consumidos NO se acumulan" no tenía por qué salir el primero. El solape léxico lo
/// desempata: cuando la pregunta y el fragmento comparten los términos concretos, ese
/// fragmento sube.
///
/// No usa el LLM a propósito. Un reordenado con modelo daría algo más de calidad, pero
/// añade una llamada por pregunta —coste y latencia en el camino crítico, que es el que
/// el agente espera en la llamada—, y este proyecto mide ambas cosas como criterio.
/// </summary>
public static class ChunkReranker
{
    /// <summary>
    /// Peso de la similitud vectorial frente al solape léxico. Alto a propósito: lo
    /// léxico desempata entre candidatos ya afines, no manda sobre el significado (si
    /// mandara, una pregunta que no comparte vocabulario con su respuesta se hundiría).
    /// </summary>
    private const double PesoVectorial = 0.75;

    /// <summary>Palabras vacías que aparecerían en casi cualquier fragmento y no discriminan.</summary>
    private static readonly HashSet<string> Vacias = new(StringComparer.OrdinalIgnoreCase)
    {
        "el","la","los","las","un","una","unos","unas","de","del","al","a","en","y","o","que",
        "se","es","son","por","para","con","sin","su","sus","lo","como","mas","más","este",
        "esta","estos","estas","cual","cuales","cuanto","cuanta","cuantos","cuantas","qué",
        "que","hay","tiene","tienen","puede","pueden","cuando","donde","si","no","ni","yo",
    };

    /// <summary>
    /// Ordena los candidatos y devuelve los <paramref name="quedarse"/> mejores.
    /// </summary>
    public static IReadOnlyList<ChunkMatch> Rerank(
        IReadOnlyList<ChunkMatch> candidatos, string pregunta, int quedarse)
    {
        if (candidatos.Count <= 1 || quedarse <= 0)
            return candidatos.Take(Math.Max(quedarse, 0)).Select(SinReordenar).ToList();

        var terminos = Terminos(pregunta);
        if (terminos.Count == 0)
            return candidatos.Take(quedarse).Select(SinReordenar).ToList();

        // El score de coseno ya viene en 0-1, así que se usa tal cual. Se probó a
        // normalizarlo min-max sobre el conjunto de candidatos y era peor: con
        // puntuaciones muy juntas (0,82 frente a 0,80, lo habitual entre fragmentos del
        // mismo documento) esa normalización estira dos milésimas hasta el rango entero
        // y el orden acaba decidiéndose por ruido en vez de por parecido real.
        return candidatos
            .Select(c => new
            {
                Chunk = c,
                Puntuacion = PesoVectorial * c.Score
                           + (1 - PesoVectorial) * Solape(c.Content, terminos),
            })
            // El score vectorial original desempata: ante igualdad, manda el significado.
            .OrderByDescending(x => x.Puntuacion)
            .ThenByDescending(x => x.Chunk.Score)
            .Take(quedarse)
            // La puntuación viaja con el fragmento: es la que explica este orden, y sin
            // ella quien mire la lista solo ve el coseno, que no lo explica.
            .Select(x => x.Chunk with { Relevance = x.Puntuacion })
            .ToList();
    }

    /// <summary>
    /// Fragmento que sale sin pasar por el reordenado: su relevancia es la similitud, sin
    /// más. Se marca explícitamente para que <c>Relevance</c> nunca quede en cero por
    /// omisión y aparezca como "sin relación" algo que sí la tiene.
    /// </summary>
    private static ChunkMatch SinReordenar(ChunkMatch c) => c with { Relevance = c.Score };

    /// <summary>Proporción de términos de la pregunta que aparecen en el fragmento.</summary>
    private static double Solape(string contenido, IReadOnlyCollection<string> terminos)
    {
        var presentes = Terminos(contenido);
        return terminos.Count(presentes.Contains) / (double)terminos.Count;
    }

    /// <summary>
    /// Palabras significativas, sin acentos ni signos. Se quitan los acentos para que
    /// "informacion" y "información" cuenten como el mismo término: el corpus los mezcla.
    /// </summary>
    private static HashSet<string> Terminos(string texto)
    {
        var limpio = new StringBuilder(texto.Length);
        foreach (var c in texto.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            limpio.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : ' ');
        }

        return limpio.ToString()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => p.Length > 2 && !Vacias.Contains(p))
            .ToHashSet(StringComparer.Ordinal);
    }
}
