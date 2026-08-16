using System.Text;
using AgentPilot.Application.Abstractions;

namespace AgentPilot.Application.Ingestion;

/// <summary>
/// Trocea respetando la estructura del Markdown en vez de cortar cada N caracteres.
///
/// El troceado ciego mezclaba en un mismo fragmento una tabla densa y la prosa que la
/// acompaña. Medido con el arnés de evals: la línea "los datos no consumidos NO se
/// acumulan…" quedaba dentro de un fragmento de ~1.000 caracteres dominado por la tabla
/// de tarifas, y el modelo no la usaba aunque el fragmento se recuperase — un fallo de
/// atención sobre el contexto, no de recuperación. Además los cortes caían a mitad de
/// un encabezado ("## Bonos" se partía en "# Bonos").
///
/// Aquí cada tabla es su propio fragmento, cada sección se corta por su encabezado, y a
/// todo fragmento se le antepone su ruta ("Documento › Sección"): un fragmento aislado
/// deja de ser un trozo de texto sin dueño y dice de qué habla, lo que ayuda tanto al
/// embedding como al modelo al leerlo.
///
/// Para prosa larga sin estructura interna, y para documentos sin Markdown en absoluto
/// (por ejemplo un PDF extraído), delega en <see cref="SlidingWindowChunker"/>: no hay
/// nada que respetar y la ventana deslizante sigue siendo la respuesta correcta.
/// </summary>
public class MarkdownAwareChunker(int chunkSize = 1000, int overlap = 200) : ITextChunker
{
    /// <summary>Separador de la ruta que encabeza cada fragmento.</summary>
    private const string Separador = " › ";

    private readonly int _chunkSize = chunkSize > 0
        ? chunkSize
        : throw new ArgumentOutOfRangeException(nameof(chunkSize));

    private readonly SlidingWindowChunker _prosaLarga = new(chunkSize, overlap);

    public IReadOnlyList<string> Split(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        text = text.Replace("\r\n", "\n").Replace('\r', '\n').Trim();

        // Sin encabezados ni tablas no hay estructura que aprovechar.
        if (!TieneEstructura(text)) return _prosaLarga.Split(text);

        var fragmentos = new List<string>();
        var rutaActual = new List<string>();   // títulos por nivel de encabezado
        var prosa = new StringBuilder();
        var lineas = text.Split('\n');

        for (int i = 0; i < lineas.Length; i++)
        {
            var linea = lineas[i];

            if (EsEncabezado(linea, out var nivel, out var titulo))
            {
                Volcar(fragmentos, prosa, rutaActual);
                ActualizarRuta(rutaActual, nivel, titulo);
                continue;
            }

            if (EsFilaDeTabla(linea))
            {
                // La tabla sale entera y sola: es lo que evita que su densidad tape la
                // prosa vecina. Se vuelca antes lo acumulado para no arrastrarlo dentro.
                Volcar(fragmentos, prosa, rutaActual);

                var tabla = new StringBuilder();
                while (i < lineas.Length && EsFilaDeTabla(lineas[i]))
                    tabla.AppendLine(lineas[i++]);
                i--; // el bucle exterior vuelve a incrementar

                foreach (var trozo in Trocear(tabla.ToString().Trim()))
                    fragmentos.Add(Componer(rutaActual, trozo));
                continue;
            }

            // Si añadir esta línea desborda el tamaño, se cierra el fragmento actual.
            if (prosa.Length + linea.Length > _chunkSize && prosa.Length > 0)
                Volcar(fragmentos, prosa, rutaActual);

            prosa.AppendLine(linea);
        }

        Volcar(fragmentos, prosa, rutaActual);
        return fragmentos;
    }

    private void Volcar(List<string> fragmentos, StringBuilder prosa, List<string> ruta)
    {
        var contenido = prosa.ToString().Trim();
        prosa.Clear();
        if (contenido.Length == 0) return;

        foreach (var trozo in Trocear(contenido))
            fragmentos.Add(Componer(ruta, trozo));
    }

    /// <summary>Parte un bloque que por sí solo excede el tamaño; si cabe, lo deja entero.</summary>
    private IReadOnlyList<string> Trocear(string bloque)
        => bloque.Length <= _chunkSize ? [bloque] : _prosaLarga.Split(bloque);

    /// <summary>Antepone la ruta de encabezados, para que el fragmento diga de qué habla.</summary>
    private static string Componer(List<string> ruta, string contenido)
    {
        var titulos = ruta.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
        return titulos.Count == 0
            ? contenido
            : $"{string.Join(Separador, titulos)}\n\n{contenido}";
    }

    /// <summary>
    /// Mantiene la jerarquía: un encabezado de nivel N sustituye al de su nivel y
    /// descarta los más profundos, que ya no aplican.
    /// </summary>
    private static void ActualizarRuta(List<string> ruta, int nivel, string titulo)
    {
        while (ruta.Count >= nivel) ruta.RemoveAt(ruta.Count - 1);
        while (ruta.Count < nivel - 1) ruta.Add(string.Empty); // saltos de nivel (# → ###)
        ruta.Add(titulo);
    }

    private static bool TieneEstructura(string text)
        => text.Split('\n').Any(l => EsEncabezado(l, out _, out _) || EsFilaDeTabla(l));

    private static bool EsEncabezado(string linea, out int nivel, out string titulo)
    {
        nivel = 0;
        titulo = string.Empty;

        var limpia = linea.TrimStart();
        while (nivel < limpia.Length && limpia[nivel] == '#') nivel++;

        // '#' pegado al texto no es encabezado en Markdown; hace falta el espacio.
        if (nivel is 0 or > 6 || nivel >= limpia.Length || limpia[nivel] != ' ') return false;

        titulo = limpia[(nivel + 1)..].Trim();
        return titulo.Length > 0;
    }

    private static bool EsFilaDeTabla(string linea)
        => linea.TrimStart().StartsWith('|');
}
