# ADR-016 — Troceado consciente de la estructura y reordenado local de la recuperación

**Estado:** Aceptada (16/08/2026)

## Contexto
Dos fallos observados, uno medido y otro visto en uso real:

1. **Caso 4 del set dorado** (*«¿Se acumulan los datos no consumidos?»*): persistía en las tres comparativas de modelo. El diagnóstico estaba escrito en [`evals/README.md`](../../evals/README.md): el dato **sí** se recuperaba, pero venía dentro de un fragmento de ~1.000 caracteres dominado por la tabla de tarifas, y el modelo no lo usaba. Fallo de atención sobre el contexto, no de búsqueda.
2. **«internet + móvil»** en producción: el asistente dijo no disponer de la lista de tarifas convergentes; tres turnos después, reformulada como «fibra + móvil», la respondió. La información estaba en el corpus y la recuperación no la había puesto delante. Lo destapó la pantalla de revisión ([ADR-015](ADR-015-valoracion-unica-por-respuesta.md)), a partir de un 👎 real.

El troceado era una ventana deslizante de 1.000 caracteres con 200 de solape, ciega a lo que cortaba: mezclaba tablas con prosa y llegaba a partir encabezados por la mitad (`## Bonos` quedaba como `# Bonos` al inicio del fragmento siguiente).

## Decisión
**Troceado por estructura** (`MarkdownAwareChunker`): cada tabla es su propio fragmento, las secciones se cortan por sus encabezados y a todo fragmento se le antepone su ruta (`Documento › Sección`) para que no sea texto sin dueño. Se delega en la ventana deslizante para prosa larga y para documentos sin Markdown (un PDF extraído), donde no hay estructura que respetar.

**Reordenado local** (`ChunkReranker`): se traen **30 candidatos** de la búsqueda vectorial y se reordenan combinando la similitud coseno (peso 0,75) con el solape léxico respecto a la pregunta (0,25), quedándose con los **10** que van al contexto. **Sin llamada al LLM**: un reordenado con modelo daría algo más de calidad a cambio de una llamada por pregunta, y la latencia del primer token es lo que el agente espera con el cliente al teléfono.

`TopK` sube de 5 a 10 y es parte de la misma decisión, no un ajuste aparte (ver más abajo).

## Consecuencias
Medido con el arnés de evals, tres pases por configuración:

| | Base | Solo troceado | Troceado + reordenado |
|---|---|---|---|
| Aciertos | 29/30 | **28/30** | **30/30** |
| Recuperación | 100 % | 96 % | 100 % |
| Exactitud de respuesta | 96 % | 92 % | **100 %** |
| Abstención | 100 % | 100 % | 100 % |
| Coste del set | $0,0078 | $0,0054 | $0,0080 |

- **Los dos cambios están acoplados y medirlos por separado fue un error.** El troceado por sí solo *empeoró* el resultado: los fragmentos bajaron de ~1.000 a ~400 caracteres y, manteniendo `TopK` en 5, el contexto que llegaba al modelo se redujo a menos de la mitad. Trocear más fino obliga a recuperar más piezas; no es una optimización independiente.
- El caso 4 pasa por primera vez, y con él el set completo.
- Coste +3 % por llevar 10 fragmentos en vez de 5. El reordenado no añade coste ni latencia apreciable: es local.
- **Reindexar es obligatorio al cambiar el troceado.** Hoy exige volver a subir los documentos, porque persistir el texto extraído ([ADR-012](ADR-012-texto-extraido-persistido.md)) sigue pendiente de implementar. Es la primera vez que ese pendiente tiene un coste real.
- La vista previa de prompt usa la misma recuperación que el chat, por el mismo motivo por el que comparte `SystemPromptBuilder` ([ADR-011](ADR-011-prompt-por-capas.md)): si recuperase distinto, dejaría de previsualizar lo que se publica.
- Descartado normalizar el coseno con min-max sobre el conjunto de candidatos: con puntuaciones muy juntas (0,82 frente a 0,80, lo habitual entre fragmentos del mismo documento) estira dos milésimas hasta el rango entero y el orden pasa a decidirse por ruido. Lo detectaron los tests unitarios del reordenador, no los evals.
