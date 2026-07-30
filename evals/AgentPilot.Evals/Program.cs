using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AgentPilot.Evals;

// Arnés de evaluación del RAG: lanza el set dorado contra la API y puntúa
// recuperación, exactitud de respuesta y abstención correcta.
//
// Uso:  dotnet run --project evals/AgentPilot.Evals [-- baseUrl usuario contraseña]

Console.OutputEncoding = Encoding.UTF8;

var baseUrl = args.ElementAtOrDefault(0) ?? "http://localhost:8080";
var username = args.ElementAtOrDefault(1) ?? "agente";
var password = args.ElementAtOrDefault(2) ?? "agente1234";

var goldenPath = LocateGoldenSet();
var golden = JsonSerializer.Deserialize<GoldenSet>(
    File.ReadAllText(goldenPath),
    new JsonSerializerOptions(JsonSerializerDefaults.Web))!;

Console.WriteLine($"Set dorado: {golden.Cases.Count} casos ({goldenPath})");
Console.WriteLine($"API: {baseUrl}  ·  usuario: {username}\n");

var api = new ApiClient(baseUrl);
await api.LoginAsync(username, password);

var results = new List<EvalResult>();
var modelsUsed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

foreach (var testCase in golden.Cases)
{
    var m = await api.AskAsync(testCase.Question);
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
    results.Add(result);
    modelsUsed.Add(m.Model);

    var mark = result.Passed ? "OK   " : "FALLO";
    Console.WriteLine($"[{mark}] #{testCase.Id,2}  {Truncate(testCase.Question, 58)}");
    if (!result.Passed)
        Console.WriteLine($"          respuesta: {Truncate(answer.Replace('\n', ' '), 90)}");
}

// --- Informe ---
var answerable = results.Where(r => r.Case.Answerable).ToList();
var notAnswerable = results.Where(r => !r.Case.Answerable).ToList();

double Pct(int hits, int total) => total == 0 ? 0 : 100.0 * hits / total;

var report = new StringBuilder();
report.AppendLine("# Resultados de evaluación (evals)");
report.AppendLine();
// Sin el modelo, el informe no es reproducible: los mismos casos dan cifras distintas según el modelo.
report.AppendLine($"- Modelo de chat: **{string.Join(", ", modelsUsed.OrderBy(x => x))}**");
report.AppendLine($"- Casos: **{results.Count}** ({answerable.Count} respondibles, {notAnswerable.Count} fuera del corpus)");
report.AppendLine($"- Aciertos globales: **{Pct(results.Count(r => r.Passed), results.Count):F1}%**");
report.AppendLine();
report.AppendLine("| Métrica | Resultado |");
report.AppendLine("|---|---|");
report.AppendLine($"| Precisión de recuperación (documento correcto citado) | **{Pct(answerable.Count(r => r.RetrievalHit), answerable.Count):F1}%** |");
report.AppendLine($"| Exactitud de la respuesta (dato clave presente) | **{Pct(answerable.Count(r => r.AnswerHit), answerable.Count):F1}%** |");
report.AppendLine($"| Abstención correcta (preguntas fuera del corpus) | **{Pct(notAnswerable.Count(r => r.AbstainedCorrectly), notAnswerable.Count):F1}%** |");
report.AppendLine($"| Fuentes en pantalla (media) | {results.Average(r => r.CitationsMs):F0} ms |");
report.AppendLine($"| Primer token de la respuesta (media) | {results.Average(r => r.FirstTokenMs):F0} ms |");
report.AppendLine($"| Primer token p95 | {Percentile(results.Select(r => (double)r.FirstTokenMs).ToList(), 0.95):F0} ms |");
report.AppendLine($"| Latencia total media | {results.Average(r => r.LatencyMs):F0} ms |");
report.AppendLine($"| Latencia total p95 | {Percentile(results.Select(r => (double)r.LatencyMs).ToList(), 0.95):F0} ms |");
report.AppendLine($"| Coste total del set | ${results.Sum(r => r.CostUsd):F4} |");
report.AppendLine($"| Coste medio por pregunta | ${results.Average(r => r.CostUsd):F6} |");
report.AppendLine();
report.AppendLine("## Detalle");
report.AppendLine();
report.AppendLine("| # | Pregunta | Recuperación | Respuesta | 1er token | Resultado |");
report.AppendLine("|---|---|---|---|---|---|");
foreach (var r in results)
{
    var retrieval = r.Case.Answerable ? (r.RetrievalHit ? "OK" : "fallo") : "—";
    var answerCell = r.Case.Answerable
        ? (r.AnswerHit ? "OK" : "fallo")
        : (r.AbstainedCorrectly ? "se abstuvo" : "NO se abstuvo");
    report.AppendLine($"| {r.Case.Id} | {r.Case.Question} | {retrieval} | {answerCell} | {r.FirstTokenMs} ms | {(r.Passed ? "**OK**" : "FALLO")} |");
}

var outputPath = Path.GetFullPath(
    Path.Combine(Path.GetDirectoryName(goldenPath)!, "..", "RESULTS.md"));
File.WriteAllText(outputPath, report.ToString());

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

// --- Utilidades ---

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

static string LocateGoldenSet()
{
    // Busca evals/golden-set/golden-set.json subiendo desde el directorio actual.
    var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (dir is not null)
    {
        var candidate = Path.Combine(dir.FullName, "evals", "golden-set", "golden-set.json");
        if (File.Exists(candidate)) return candidate;
        dir = dir.Parent;
    }
    throw new FileNotFoundException("No se encontró evals/golden-set/golden-set.json");
}
