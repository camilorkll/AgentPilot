using AgentPilot.Application.Abstractions;

namespace AgentPilot.Application.Ingestion;

/// <summary>
/// Trocea texto con una ventana deslizante: fragmentos de ~ChunkSize caracteres
/// que se solapan en Overlap caracteres, cortando en un límite natural (fin de
/// párrafo o de frase) cercano para no partir ideas por la mitad.
/// </summary>
public class SlidingWindowChunker(int chunkSize = 1000, int overlap = 200) : ITextChunker
{
    private readonly int _chunkSize = chunkSize > 0
        ? chunkSize
        : throw new ArgumentOutOfRangeException(nameof(chunkSize));

    private readonly int _overlap = overlap >= 0 && overlap < chunkSize
        ? overlap
        : throw new ArgumentOutOfRangeException(nameof(overlap),
            "El solapamiento debe ser >= 0 y menor que el tamaño de trozo.");

    public IReadOnlyList<string> Split(string text)
    {
        var chunks = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return chunks;

        // Normaliza saltos de línea para que la detección de límites sea fiable.
        text = text.Replace("\r\n", "\n").Replace('\r', '\n').Trim();

        if (text.Length <= _chunkSize)
        {
            chunks.Add(text);
            return chunks;
        }

        int start = 0;
        while (start < text.Length)
        {
            int end = Math.Min(start + _chunkSize, text.Length);

            // Si no es el final del texto, intenta cerrar el trozo en un límite
            // natural situado en la segunda mitad de la ventana.
            if (end < text.Length)
            {
                int boundary = FindBoundary(text, start + _chunkSize / 2, end);
                if (boundary > start) end = boundary;
            }

            var chunk = text[start..end].Trim();
            if (chunk.Length > 0) chunks.Add(chunk);

            if (end >= text.Length) break;

            // Retrocede 'overlap' para que el siguiente trozo repita el final del
            // actual. El ternario garantiza que 'start' siempre avanza.
            int nextStart = end - _overlap;
            start = nextStart > start ? nextStart : end;
        }

        return chunks;
    }

    /// <summary>Último fin de párrafo (\n) o de frase (. ! ?) en el rango [from, to).</summary>
    private static int FindBoundary(string text, int from, int to)
    {
        int paragraph = text.LastIndexOf('\n', to - 1, to - from);
        if (paragraph >= from) return paragraph + 1;

        for (int i = to - 1; i >= from; i--)
        {
            if (text[i] is '.' or '!' or '?'
                && i + 1 < text.Length && char.IsWhiteSpace(text[i + 1]))
                return i + 1;
        }
        return -1;
    }
}
