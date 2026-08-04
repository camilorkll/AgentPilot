using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AgentPilot.Evals;

// Arnés de evaluación del RAG: lanza el set dorado contra la API y puntúa
// recuperación, exactitud de respuesta y abstención correcta.
//
// Modo por defecto (una campaña, un set — comportamiento histórico):
//   dotnet run --project evals/AgentPilot.Evals [-- baseUrl usuario contraseña campaña]
//
// Todas las campañas del manifiesto evals/golden-set/campaigns.json en una pasada,
// con un informe comparado (RESULTS-<campaña>.md + RESULTS-CAMPAIGNS.md):
//   dotnet run --project evals/AgentPilot.Evals -- all [baseUrl usuario contraseña]
//
// Comprobación de aislamiento entre campañas (requiere rol admin): preguntas
// respondibles de TeleNova formuladas en otra campaña deben abstenerse, y solo en
// TeleNova deben responder. Sin un quinto argumento, crea y destruye una campaña
// vacía; con él, prueba contra una campaña real ya existente (ver la nota sobre
// datos exclusivos más abajo):
//   dotnet run --project evals/AgentPilot.Evals -- isolation [baseUrl admin adminPass] [otraCampañaId]

Console.OutputEncoding = Encoding.UTF8;

var modo = args.ElementAtOrDefault(0)?.ToLowerInvariant();
if (modo is "all" or "isolation")
{
    var resto = args.Skip(1).ToArray();
    var baseUrlModo = resto.ElementAtOrDefault(0) ?? "http://localhost:8080";

    return modo == "all"
        ? await RunAllAsync(baseUrlModo, resto.ElementAtOrDefault(1) ?? "agente", resto.ElementAtOrDefault(2) ?? "agente1234")
        : await RunIsolationAsync(
            baseUrlModo, resto.ElementAtOrDefault(1) ?? "admin", resto.ElementAtOrDefault(2) ?? "admin1234",
            resto.ElementAtOrDefault(3) is { } id ? Guid.Parse(id) : null);
}

// --- Modo por defecto: una campaña, un set (comportamiento histórico) ---

var baseUrl = args.ElementAtOrDefault(0) ?? "http://localhost:8080";
var username = args.ElementAtOrDefault(1) ?? "agente";
var password = args.ElementAtOrDefault(2) ?? "agente1234";

// El asistente responde siempre dentro de una campaña, así que el arnés también.
// Por defecto, la campaña "TeleNova" que crea la migración con Guid fijo: es la que
// contiene el corpus del set dorado.
var campaignId = Guid.Parse(
    args.ElementAtOrDefault(3) ?? "11111111-1111-1111-1111-111111111111");

var goldenPath = Path.Combine(LocateGoldenSetDir(), "golden-set.json");
Console.WriteLine($"API: {baseUrl}  ·  usuario: {username}  ·  campaña: {campaignId}\n");

var api = new ApiClient(baseUrl);
await api.LoginAsync(username, password);

var (results, modelsUsed) = await RunGoldenSetAsync(api, campaignId, goldenPath);

var outputPath = Path.GetFullPath(Path.Combine(LocateGoldenSetDir(), "..", "RESULTS.md"));
File.WriteAllText(outputPath, BuildReport(results, modelsUsed));

var answerable = results.Where(r => r.Case.Answerable).ToList();
var notAnswerable = results.Where(r => !r.Case.Answerable).ToList();

Console.WriteLine();
Console.WriteLine($"Aciertos: {results.Count(r => r.Passed)}/{results.Count}  ·  informe: {outputPath}");
Console.WriteLine($"Recuperación {Pct(answerable.Count(r => r.RetrievalHit), answerable.Count):F1}%  ·  " +
                  $"Respuesta {Pct(answerable.Count(r => r.AnswerHit), answerable.Count):F1}%  ·  " +
                  $"Abstención {Pct(notAnswerable.Count(r => r.AbstainedCorrectly), notAnswerable.Count):F1}%  ·  " +
                  $"Coste ${results.Sum(r => r.CostUsd):F4}");
Console.WriteLine($"Modelo {string.Join(", ", modelsUsed)}  ·  " +
                  $"fuentes {results.Average(r => r.CitationsMs):F0} ms  ·  " +
                  $"primer token {results.Average(r => r.FirstTokenMs):F0} ms  ·  " +
                  $"total {results.Average(r => r.LatencyMs):F0} ms");

return results.All(r => r.Passed) ? 0 : 1;

// --- Modos adicionales ---

/// <summary>
/// Ejecuta cada entrada del manifiesto contra su propia campaña, con una sola sesión, y
/// escribe un informe comparado además del detalle de cada una. Añadir una campaña al
/// manifiesto (por ejemplo, «Luz y Gas Premium» en el paso 8.7) no requiere tocar código.
/// </summary>
async Task<int> RunAllAsync(string baseUrlAll, string userAll, string passAll)
{
    var dir = LocateGoldenSetDir();
    var manifest = JsonSerializer.Deserialize<List<CampaignEntry>>(
        File.ReadAllText(Path.Combine(dir, "campaigns.json")),
        new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    var apiAll = new ApiClient(baseUrlAll);
    await apiAll.LoginAsync(userAll, passAll);

    var summary = new StringBuilder();
    summary.AppendLine("# Comparativa entre campañas (evals)");
    summary.AppendLine();
    summary.AppendLine("| Campaña | Aciertos | Recuperación | Abstención | Coste del set |");
    summary.AppendLine("|---|---|---|---|---|");

    var todoOk = true;
    foreach (var entry in manifest)
    {
        Console.WriteLine($"\n##### Campaña: {entry.Label} #####");
        var (resultsEntry, modelsEntry) = await RunGoldenSetAsync(
            apiAll, entry.CampaignId, Path.Combine(dir, entry.GoldenSet));

        File.WriteAllText(
            Path.GetFullPath(Path.Combine(dir, "..", $"RESULTS-{entry.Label}.md")),
            BuildReport(resultsEntry, modelsEntry));

        var answerableEntry = resultsEntry.Where(r => r.Case.Answerable).ToList();
        var notAnswerableEntry = resultsEntry.Where(r => !r.Case.Answerable).ToList();
        summary.AppendLine(
            $"| {entry.Label} | {Pct(resultsEntry.Count(r => r.Passed), resultsEntry.Count):F1}% | " +
            $"{Pct(answerableEntry.Count(r => r.RetrievalHit), answerableEntry.Count):F1}% | " +
            $"{Pct(notAnswerableEntry.Count(r => r.AbstainedCorrectly), notAnswerableEntry.Count):F1}% | " +
            $"${resultsEntry.Sum(r => r.CostUsd):F4} |");

        todoOk &= resultsEntry.All(r => r.Passed);
    }

    var summaryPath = Path.GetFullPath(Path.Combine(dir, "..", "RESULTS-CAMPAIGNS.md"));
    File.WriteAllText(summaryPath, summary.ToString());
    Console.WriteLine($"\nResumen comparado: {summaryPath}");
    return todoOk ? 0 : 1;
}

/// <summary>
/// Comprobación automatizada de aislamiento: preguntas respondibles de TeleNova, con un
/// dato ancla exclusivo (nombre de producto, nunca un concepto genérico), formuladas en
/// otra campaña. Deben abstenerse ahí y responder con cita solo en TeleNova.
///
/// Es la contraparte automatizada de <c>ChunkSearchTests.Busqueda_NuncaDevuelveFragmentosDeOtraCampaña</c>
/// (que prueba la SQL) y de la verificación manual hecha en los pasos 8.3 y 8.5 (que probó
/// la API real): aquí queda como parte del arnés, repetible y con código de salida para CI.
///
/// Si no se indica una campaña existente, crea una vacía y la destruye al terminar
/// (Activa → Inactiva → Cerrada → eliminada), incluso si alguna aserción falla. Contra una
/// campaña real con su propio corpus (p. ej. «Luz y Gas Premium» del paso 8.7), cuidado:
/// una pregunta cuyo dato también exista allí con otro valor no demuestra fuga, demuestra
/// que el instrumento de medida está mal elegido — por eso los casos de abajo se anclan a
/// nombres de producto de TeleNova (Nova Mini, NovaMesh, Nova Infinita, Bono Viaje), que
/// ninguna otra campaña puede compartir por definición.
/// </summary>
async Task<int> RunIsolationAsync(string baseUrlIso, string userIso, string passIso, Guid? campañaAjenaExistente)
{
    const string TeleNovaId = "11111111-1111-1111-1111-111111111111";
    var teleNova = Guid.Parse(TeleNovaId);

    var golden = JsonSerializer.Deserialize<GoldenSet>(
        File.ReadAllText(Path.Combine(LocateGoldenSetDir(), "golden-set.json")),
        new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

    // Casos anclados a un nombre de producto de TeleNova: no hay corpus con el que puedan
    // coincidir por casualidad, así que una respuesta ahí SIEMPRE sería una fuga real.
    int[] idsAncladosAProducto = [1, 16, 19, 20]; // Nova Mini, NovaMesh, Nova Infinita, Bono Viaje
    var casos = golden.Cases.Where(c => idsAncladosAProducto.Contains(c.Id)).ToList();
    if (casos.Count != idsAncladosAProducto.Length)
        throw new InvalidOperationException(
            "El golden-set ya no contiene los casos esperados para el aislamiento; revisa idsAncladosAProducto.");

    var apiIso = new ApiClient(baseUrlIso);
    await apiIso.LoginAsync(userIso, passIso);

    Guid ajena;
    var esEfímera = campañaAjenaExistente is null;
    if (esEfímera)
    {
        ajena = await apiIso.CreateCampaignAsync("Aislamiento (evals) — borrar si persiste");
        Console.WriteLine($"Campaña efímera creada: {ajena}\n");
    }
    else
    {
        ajena = campañaAjenaExistente!.Value;
        Console.WriteLine($"Usando campaña existente: {ajena}\n");
    }

    var fallos = new List<string>();
    try
    {
        foreach (var caso in casos)
        {
            // 1) En la campaña ajena: no debe citar nada ni responder con el dato.
            var enAjena = await apiIso.AskAsync(caso.Question, ajena);
            var fugó = enAjena.Documents.Length > 0 || !LooksLikeAbstention(enAjena.Answer);
            Console.WriteLine($"[{(fugó ? "FUGA " : "OK   ")}] #{caso.Id,2} en campaña ajena  {Truncate(caso.Question, 50)}");
            if (fugó)
            {
                fallos.Add($"#{caso.Id} respondió en la campaña ajena: \"{Truncate(enAjena.Answer, 90)}\"");
                Console.WriteLine($"          respuesta: {Truncate(enAjena.Answer.Replace('\n', ' '), 90)}");
            }

            // 2) En TeleNova: la contraparte. Si esto fallara, el problema no sería
            //    aislamiento sino que el propio dato dejó de estar disponible.
            var enTeleNova = await apiIso.AskAsync(caso.Question, teleNova);
            var retrievalOk = caso.ExpectedDocument is null ||
                enTeleNova.Documents.Any(d => d.Contains(caso.ExpectedDocument, StringComparison.OrdinalIgnoreCase));
            var normalized = NormalizeNumbers(enTeleNova.Answer);
            var answerOk = caso.ExpectedKeywords.Length == 0 ||
                caso.ExpectedKeywords.Any(k => normalized.Contains(k, StringComparison.OrdinalIgnoreCase));
            var mal = !retrievalOk || !answerOk;
            Console.WriteLine($"[{(mal ? "FALLO" : "OK   ")}] #{caso.Id,2} en TeleNova       {Truncate(caso.Question, 50)}");
            if (mal)
                fallos.Add($"#{caso.Id} no respondió correctamente en TeleNova (su propia campaña).");
        }
    }
    finally
    {
        if (esEfímera)
        {
            // Se destruye siempre, incluso si alguna aserción falló arriba: no dejar
            // campañas de prueba huérfanas es tan importante como el resultado del test.
            await apiIso.SetCampaignStatusAsync(ajena, "inactive");
            await apiIso.SetCampaignStatusAsync(ajena, "closed");
            await apiIso.DeleteCampaignAsync(ajena);
            Console.WriteLine($"\nCampaña efímera {ajena} eliminada.");
        }
    }

    var reportIso = new StringBuilder();
    reportIso.AppendLine("# Aislamiento entre campañas (evals)");
    reportIso.AppendLine();
    reportIso.AppendLine($"- Casos: **{casos.Count}**, anclados a nombres de producto exclusivos de TeleNova");
    reportIso.AppendLine($"- Resultado: **{(fallos.Count == 0 ? "sin fugas" : $"{fallos.Count} problema(s)")}**");
    if (fallos.Count > 0)
    {
        reportIso.AppendLine();
        foreach (var f in fallos) reportIso.AppendLine($"- {f}");
    }
    var pathIso = Path.GetFullPath(Path.Combine(LocateGoldenSetDir(), "..", "ISOLATION-RESULTS.md"));
    File.WriteAllText(pathIso, reportIso.ToString());

    Console.WriteLine($"\nInforme: {pathIso}");
    Console.WriteLine(fallos.Count == 0
        ? "Sin fugas: ninguna pregunta respondible de TeleNova se contestó fuera de ella."
        : $"{fallos.Count} problema(s) — ver detalle arriba.");

    return fallos.Count == 0 ? 0 : 1;
}

// --- Núcleo compartido: ejecutar un set dorado contra una campaña ---

async Task<(List<EvalResult> Results, HashSet<string> Models)> RunGoldenSetAsync(
    ApiClient apiClient, Guid campaña, string ruta)
{
    var golden = JsonSerializer.Deserialize<GoldenSet>(
        File.ReadAllText(ruta), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    Console.WriteLine($"Set dorado: {golden.Cases.Count} casos ({ruta})");

    var resultsLocal = new List<EvalResult>();
    var modelsLocal = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    foreach (var testCase in golden.Cases)
    {
        var m = await apiClient.AskAsync(testCase.Question, campaña);
        var (answer, documents) = (m.Answer, m.Documents);

        var retrievalHit = testCase.ExpectedDocument is null ||
            documents.Any(d => d.Contains(testCase.ExpectedDocument, StringComparison.OrdinalIgnoreCase));

        var normalized = NormalizeNumbers(answer);
        var answerHit = testCase.ExpectedKeywords.Length == 0 ||
            testCase.ExpectedKeywords.Any(k => normalized.Contains(k, StringComparison.OrdinalIgnoreCase));

        var abstained = LooksLikeAbstention(answer);

        var result = new EvalResult
        {
            Case = testCase,
            Answer = answer,
            CitedDocuments = documents,
            LatencyMs = m.LatencyMs,
            CitationsMs = m.CitationsMs,
            FirstTokenMs = m.FirstTokenMs,
            CostUsd = m.CostUsd,
            RetrievalHit = retrievalHit,
            AnswerHit = answerHit,
            AbstainedCorrectly = !testCase.Answerable && abstained,
        };
        resultsLocal.Add(result);
        modelsLocal.Add(m.Model);

        var mark = result.Passed ? "OK   " : "FALLO";
        Console.WriteLine($"[{mark}] #{testCase.Id,2}  {Truncate(testCase.Question, 58)}");
        if (!result.Passed)
            Console.WriteLine($"          respuesta: {Truncate(answer.Replace('\n', ' '), 90)}");
    }

    return (resultsLocal, modelsLocal);
}

// --- Informe ---

string BuildReport(List<EvalResult> resultsToReport, HashSet<string> modelsToReport)
{
    var answerableR = resultsToReport.Where(r => r.Case.Answerable).ToList();
    var notAnswerableR = resultsToReport.Where(r => !r.Case.Answerable).ToList();

    var report = new StringBuilder();
    report.AppendLine("# Resultados de evaluación (evals)");
    report.AppendLine();
    // Sin el modelo, el informe no es reproducible: los mismos casos dan cifras distintas según el modelo.
    report.AppendLine($"- Modelo de chat: **{string.Join(", ", modelsToReport.OrderBy(x => x))}**");
    report.AppendLine($"- Casos: **{resultsToReport.Count}** ({answerableR.Count} respondibles, {notAnswerableR.Count} fuera del corpus)");
    report.AppendLine($"- Aciertos globales: **{Pct(resultsToReport.Count(r => r.Passed), resultsToReport.Count):F1}%**");
    report.AppendLine();
    report.AppendLine("| Métrica | Resultado |");
    report.AppendLine("|---|---|");
    report.AppendLine($"| Precisión de recuperación (documento correcto citado) | **{Pct(answerableR.Count(r => r.RetrievalHit), answerableR.Count):F1}%** |");
    report.AppendLine($"| Exactitud de la respuesta (dato clave presente) | **{Pct(answerableR.Count(r => r.AnswerHit), answerableR.Count):F1}%** |");
    report.AppendLine($"| Abstención correcta (preguntas fuera del corpus) | **{Pct(notAnswerableR.Count(r => r.AbstainedCorrectly), notAnswerableR.Count):F1}%** |");
    report.AppendLine($"| Fuentes en pantalla (media) | {resultsToReport.Average(r => r.CitationsMs):F0} ms |");
    report.AppendLine($"| Primer token de la respuesta (media) | {resultsToReport.Average(r => r.FirstTokenMs):F0} ms |");
    report.AppendLine($"| Primer token p95 | {Percentile(resultsToReport.Select(r => (double)r.FirstTokenMs).ToList(), 0.95):F0} ms |");
    report.AppendLine($"| Latencia total media | {resultsToReport.Average(r => r.LatencyMs):F0} ms |");
    report.AppendLine($"| Latencia total p95 | {Percentile(resultsToReport.Select(r => (double)r.LatencyMs).ToList(), 0.95):F0} ms |");
    report.AppendLine($"| Coste total del set | ${resultsToReport.Sum(r => r.CostUsd):F4} |");
    report.AppendLine($"| Coste medio por pregunta | ${resultsToReport.Average(r => r.CostUsd):F6} |");
    report.AppendLine();
    report.AppendLine("## Detalle");
    report.AppendLine();
    report.AppendLine("| # | Pregunta | Recuperación | Respuesta | 1er token | Resultado |");
    report.AppendLine("|---|---|---|---|---|---|");
    foreach (var r in resultsToReport)
    {
        var retrieval = r.Case.Answerable ? (r.RetrievalHit ? "OK" : "fallo") : "—";
        var answerCell = r.Case.Answerable
            ? (r.AnswerHit ? "OK" : "fallo")
            : (r.AbstainedCorrectly ? "se abstuvo" : "NO se abstuvo");
        report.AppendLine($"| {r.Case.Id} | {r.Case.Question} | {retrieval} | {answerCell} | {r.FirstTokenMs} ms | {(r.Passed ? "**OK**" : "FALLO")} |");
    }
    return report.ToString();
}

// --- Utilidades ---

double Pct(int hits, int total) => total == 0 ? 0 : 100.0 * hits / total;

/// <summary>
/// Convierte los números escritos con letras en dígitos antes de puntuar. "Cinco intentos"
/// y "5 intentos" son el mismo dato: sin esta normalización el corrector penalizaba el
/// estilo de redacción del modelo en lugar de su exactitud, lo que hacía la comparación
/// entre modelos injusta (uno tiende a escribir cifras y otro a escribirlas con letras).
/// </summary>
static string NormalizeNumbers(string answer)
{
    (string Word, string Digit)[] números =
    [
        ("cero", "0"), ("uno", "1"), ("una", "1"), ("dos", "2"), ("tres", "3"),
        ("cuatro", "4"), ("cinco", "5"), ("seis", "6"), ("siete", "7"), ("ocho", "8"),
        ("nueve", "9"), ("diez", "10"), ("once", "11"), ("doce", "12"), ("trece", "13"),
        ("catorce", "14"), ("quince", "15"), ("dieciséis", "16"), ("diecisiete", "17"),
        ("dieciocho", "18"), ("diecinueve", "19"), ("veinte", "20"), ("treinta", "30"),
        ("cuarenta", "40"), ("cincuenta", "50"), ("sesenta", "60"), ("cien", "100"),
    ];

    // Se añaden los dígitos en lugar de sustituir, para no romper una respuesta que ya
    // los use ni depender de tildes o mayúsculas.
    var extra = new StringBuilder(answer);
    foreach (var (word, digit) in números)
        if (Regex.IsMatch(answer, $@"\b{word}\b", RegexOptions.IgnoreCase))
            extra.Append($" {digit}");

    return extra.ToString();
}

static bool LooksLikeAbstention(string answer)
{
    string[] señales =
    [
        "no dispongo", "no tengo", "no hay información", "no se encuentra",
        "no aparece", "no consta", "no puedo responder", "no figura",
    ];
    return señales.Any(s => answer.Contains(s, StringComparison.OrdinalIgnoreCase));
}

static double Percentile(List<double> values, double percentile)
{
    if (values.Count == 0) return 0;
    values.Sort();
    var index = (int)Math.Ceiling(values.Count * percentile) - 1;
    return values[Math.Clamp(index, 0, values.Count - 1)];
}

static string Truncate(string text, int max) =>
    text.Length <= max ? text : text[..max] + "…";

static string LocateGoldenSetDir()
{
    // Busca evals/golden-set/ subiendo desde el directorio actual.
    var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (dir is not null)
    {
        var candidate = Path.Combine(dir.FullName, "evals", "golden-set");
        if (File.Exists(Path.Combine(candidate, "golden-set.json"))) return candidate;
        dir = dir.Parent;
    }
    throw new FileNotFoundException("No se encontró evals/golden-set/golden-set.json");
}
