namespace AgentPilot.Application.Ingestion;

/// <summary>
/// Qué se ha encolado para reindexar y qué se ha quedado fuera.
///
/// Los omitidos se devuelven con su motivo en vez de ignorarse en silencio: si un
/// documento no se puede reindexar porque se ingirió antes de que se guardara su texto,
/// quien lo pide tiene que enterarse en ese momento —para volver a subirlo— y no
/// meses después, al notar que ese documento responde peor que los demás.
/// </summary>
/// <param name="Encolados">Documentos que se van a reindexar en segundo plano.</param>
/// <param name="Omitidos">Documentos que no se pueden reindexar, con el porqué.</param>
public sealed record ReindexResult(
    IReadOnlyList<Guid> Encolados,
    IReadOnlyList<DocumentoOmitido> Omitidos);

/// <param name="Motivo">Explicación en lenguaje llano, apta para mostrarse tal cual.</param>
public sealed record DocumentoOmitido(Guid DocumentId, string FileName, string Motivo);
