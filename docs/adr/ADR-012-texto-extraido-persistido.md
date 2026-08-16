# ADR-012 — Persistir el texto extraído del documento

**Estado:** Aceptada (04/08/2026) — **implementada** (16/08/2026)

## Contexto
Cambiar de modelo de *embeddings* (por ejemplo, de OpenAI `text-embedding-3-small` a Ollama `nomic-embed-text`) exige reindexar todo el corpus: los vectores de dos modelos no son comparables entre sí (fallo semántico, sin excepción ni log) y además tienen dimensión distinta (1536 frente a 768), fijada en la columna `vector(1536)` de Postgres (fallo físico, el `INSERT` falla directamente). Hoy `Documento` solo guarda sus fragmentos ya vectorizados, no el texto original: reindexar obligaría a que alguien conservara y volviera a subir a mano cada PDF o Markdown ya ingerido.

## Decisión
Persistir el texto extraído de cada documento en una nueva columna `documents.ExtractedText`, poblada en el mismo paso de la ingesta que hoy genera los fragmentos. Con eso, reindexar (cambio de modelo de *embeddings* o mejora futura del troceado) se convierte en: borrar los fragmentos existentes, volver a aplicar el *chunker* sobre el texto ya guardado y regenerar los *embeddings* — sin depender de que los ficheros originales sigan disponibles en ningún sitio.

## Consecuencias
- Coste: una columna de texto más por documento; sin impacto en el modelo de búsqueda (`ExtractedText` no se indexa ni se usa en la recuperación, solo sirve para reindexar).
- Habilita también mejorar la estrategia de troceado en el futuro (por ejemplo, un *chunking* consciente de la estructura Markdown que separe tablas de prosa — ver el fallo del caso 4 en [`evals/COMPARATIVA-MODELOS.md`](../../evals/COMPARATIVA-MODELOS.md)) sin re-subir nada.
- **Se aplazó a propósito y el aplazamiento fue correcto**: cuando se escribió no había ninguna necesidad operativa de reindexar (un único modelo de *embeddings* global, ADR-010), así que se documentó la decisión sin construir maquinaria que nadie usaría.

## Implementación (16/08/2026)
Lo que disparó construirlo fue el troceado por estructura ([ADR-016](ADR-016-troceado-estructural-y-reordenado.md)): obligó a reindexar dos veces en un día, y solo se pudo hacer **por casualidad**, porque el corpus de este proyecto vive en el repositorio y se pudieron volver a subir los ficheros. En un despliegue real, donde los documentos los sube un administrador desde su equipo, esa vía no existe.

- `documents.ExtractedText`, poblada en `MarcarIndexado`, que exige el texto como parámetro obligatorio: así es imposible dejar un documento indexado sin la única fuente que permite regenerarlo, y el descuido no se descubriría meses después.
- `POST /documents/reindex` encola el reindexado de una campaña por la misma cola que la ingesta. Un trabajo con `Content` a null **es** el reindexado: el trabajo pesado (trocear, vectorizar, indexar) es idéntico, solo cambia de dónde sale el texto.
- Los documentos anteriores a esta columna **no se pueden reindexar** y se devuelven en `skipped` con el motivo. No se intenta reconstruir su texto a partir de los fragmentos: están solapados a propósito, así que recomponerlos daría contenido duplicado y sucio — peor que no tenerlo, porque el resultado parecería válido.
- El estado `Ready → Processing` solo lo permite `MarcarReindexando`, aparte de `MarcarProcesando`, para que reindexar sea una intención explícita y no un efecto colateral de reprocesar.
- Sigue **pendiente** el otro escenario que motivó este ADR: cambiar de modelo de *embeddings* exige además que la columna `vector(1536)` deje de estar fijada a esa dimensión (Ollama produce 768). El reindexado ya está; la dimensión variable, no.
