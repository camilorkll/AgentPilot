using AgentPilot.Application.Ingestion;

namespace AgentPilot.Application.Tests;

/// <summary>
/// El troceado condiciona lo que el modelo llega a leer, así que estos tests fijan las
/// tres garantías que motivaron el cambio: una tabla no comparte fragmento con la prosa
/// que la acompaña, ningún corte parte un encabezado, y todo fragmento dice de qué
/// sección viene.
/// </summary>
public class MarkdownAwareChunkerTests
{
    /// <summary>Recorte del documento real que hacía fallar el caso 4 de los evals.</summary>
    private const string CatalogoTarifas = """
        # Catálogo de tarifas móviles TeleNova

        ## Tarifas particulares

        | Tarifa | Datos | Precio/mes |
        |---|---|---|
        | Nova Mini | 10 GB | 9,90 € |
        | Nova Max | 120 GB | 19,90 € |

        - Todas las tarifas incluyen SMS ilimitados.
        - Los datos no consumidos NO se acumulan al mes siguiente, excepto en Nova Max.

        ## Bonos adicionales

        - Bono 5 GB: 5 € (caduca a los 30 días).
        """;

    [Fact]
    public void LaTabla_NoCompartefragmentoConLaProsaDeSuSeccion()
    {
        var fragmentos = new MarkdownAwareChunker().Split(CatalogoTarifas);

        var conTabla = fragmentos.Single(f => f.Contains("Nova Mini | 10 GB"));
        var conAcumulacion = fragmentos.Single(f => f.Contains("NO se acumulan"));

        // Es el arreglo del caso 4: el dato dejaba de usarse cuando competía con la
        // densidad de la tabla dentro del mismo fragmento.
        Assert.NotEqual(conTabla, conAcumulacion);
        Assert.DoesNotContain("9,90 €", conAcumulacion);
    }

    [Fact]
    public void CadaFragmento_LlevaSuRutaDeEncabezados()
    {
        var fragmentos = new MarkdownAwareChunker().Split(CatalogoTarifas);

        var conAcumulacion = fragmentos.Single(f => f.Contains("NO se acumulan"));
        var conBono = fragmentos.Single(f => f.Contains("Bono 5 GB"));

        // Un fragmento suelto tiene que decir de qué habla: ayuda al embedding y al
        // modelo, que de otro modo ve una lista de guiones sin dueño.
        Assert.StartsWith("Catálogo de tarifas móviles TeleNova › Tarifas particulares", conAcumulacion);
        Assert.StartsWith("Catálogo de tarifas móviles TeleNova › Bonos adicionales", conBono);
    }

    [Fact]
    public void NingunFragmento_PierdeUnEncabezadoPorLaMitad()
    {
        var fragmentos = new MarkdownAwareChunker().Split(CatalogoTarifas);

        // La ventana deslizante cortaba "## Bonos adicionales" y dejaba "# Bonos
        // adicionales" al principio del siguiente fragmento.
        Assert.DoesNotContain(fragmentos, f => f.Contains("\n# Bonos"));
    }

    [Fact]
    public void SinEncabezadosNiTablas_SeComportaComoLaVentanaDeslizante()
    {
        var prosa = string.Join(" ", Enumerable.Repeat("Texto plano sin ninguna estructura.", 120));

        var conEstructura = new MarkdownAwareChunker(chunkSize: 300, overlap: 50).Split(prosa);
        var deslizante = new SlidingWindowChunker(chunkSize: 300, overlap: 50).Split(prosa);

        // Un PDF extraído no trae Markdown; ahí no hay nada que respetar.
        Assert.Equal(deslizante, conEstructura);
    }

    [Fact]
    public void UnaSeccionMuyLarga_SeParteSinPerderLaRuta()
    {
        var largo = "# Documento\n\n## Sección extensa\n\n"
            + string.Join("\n", Enumerable.Repeat("Una línea de prosa bastante larga para ir sumando.", 60));

        var fragmentos = new MarkdownAwareChunker(chunkSize: 400, overlap: 50).Split(largo);

        Assert.True(fragmentos.Count > 1);
        Assert.All(fragmentos, f => Assert.StartsWith("Documento › Sección extensa", f));
    }

    [Fact]
    public void SaltoDeNivel_NoDejaHuecosNiConfundeLaJerarquia()
    {
        // De '#' directamente a '###', sin '##' por medio.
        var fragmentos = new MarkdownAwareChunker()
            .Split("# Título\n\n### Subsección\n\nContenido de la subsección.");

        var unico = Assert.Single(fragmentos);
        Assert.StartsWith("Título › Subsección", unico);
    }

    [Fact]
    public void TextoVacio_NoProduceFragmentos()
    {
        Assert.Empty(new MarkdownAwareChunker().Split("   \n  "));
    }
}
