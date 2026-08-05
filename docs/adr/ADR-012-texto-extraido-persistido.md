# ADR-012 — Persistir el texto extraído del documento

**Estado:** Aceptada (04/08/2026) — **pendiente de implementar** (línea futura, no forma parte del alcance ejecutado de la Fase 8)

## Contexto
Cambiar de modelo de *embeddings* (por ejemplo, de OpenAI `text-embedding-3-small` a Ollama `nomic-embed-text`) exige reindexar todo el corpus: los vectores de dos modelos no son comparables entre sí (fallo semántico, sin excepción ni log) y además tienen dimensión distinta (1536 frente a 768), fijada en la columna `vector(1536)` de Postgres (fallo físico, el `INSERT` falla directamente). Hoy `Documento` solo guarda sus fragmentos ya vectorizados, no el texto original: reindexar obligaría a que alguien conservara y volviera a subir a mano cada PDF o Markdown ya ingerido.

## Decisión
Persistir el texto extraído de cada documento en una nueva columna `documents.ExtractedText`, poblada en el mismo paso de la ingesta que hoy genera los fragmentos. Con eso, reindexar (cambio de modelo de *embeddings* o mejora futura del troceado) se convierte en: borrar los fragmentos existentes, volver a aplicar el *chunker* sobre el texto ya guardado y regenerar los *embeddings* — sin depender de que los ficheros originales sigan disponibles en ningún sitio.

## Consecuencias
- Coste: una columna de texto más por documento; sin impacto en el modelo de búsqueda (`ExtractedText` no se indexa ni se usa en la recuperación, solo sirve para reindexar).
- Habilita también mejorar la estrategia de troceado en el futuro (por ejemplo, un *chunking* consciente de la estructura Markdown que separe tablas de prosa — ver el fallo del caso 4 en [`evals/COMPARATIVA-MODELOS.md`](../../evals/COMPARATIVA-MODELOS.md)) sin re-subir nada.
- **No implementado en esta fase**: no hay hoy ninguna necesidad operativa de reindexar (un único modelo de *embeddings* global, ADR-010), así que se documenta la decisión y se deja como línea futura en el README en vez de construir una migración y un flujo de reindexado que nadie usaría todavía.
