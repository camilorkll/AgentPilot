# Evaluación del RAG (evals)

Medición objetiva de la calidad del sistema RAG sobre el corpus de TeleNova,
en lugar de una valoración subjetiva ("parece que responde bien").

## Qué se mide

| Métrica | Qué comprueba | Por qué importa |
|---|---|---|
| **Precisión de recuperación** | El documento correcto aparece entre las citas | Mide la calidad del *retrieval* (embeddings + búsqueda vectorial) |
| **Exactitud de la respuesta** | La respuesta contiene el dato clave esperado | Mide que el modelo usa bien el contexto recuperado |
| **Abstención correcta** | Ante preguntas fuera del corpus, dice que no dispone de la información | Mide el *grounding*: que **no alucina** |
| Latencia (media y p95) y coste | Rendimiento y economía por pregunta | Viabilidad operativa (LLMOps) |

## Set dorado

[`golden-set/golden-set.json`](golden-set/golden-set.json) contiene **30 casos**:

- **1–25**: preguntas con respuesta en el corpus. Cada una declara el documento
  fuente esperado y una lista de datos clave (basta que aparezca uno).
- **26–30**: preguntas **fuera del corpus** (horarios de tienda, número de empleados,
  TV de pago…). El resultado correcto es que el asistente **se abstenga**.

## Cómo ejecutarlo

Con el stack levantado (`docker compose up -d`) y el corpus ingerido:

```bash
dotnet run --project evals/AgentPilot.Evals
```

Parámetros opcionales (URL, usuario, contraseña):

```bash
dotnet run --project evals/AgentPilot.Evals -- http://localhost:8080 agente agente1234
```

El arnés autentica, lanza cada pregunta consumiendo el stream SSE, puntúa los
resultados y escribe el informe en [`RESULTS.md`](RESULTS.md). Devuelve código de
salida 0 si todos los casos pasan (útil para integrarlo en CI).

## Comparar modelos

Para comparar calidad/coste entre modelos, cambia `OPENAI_CHAT_MODEL` en `.env`,
reconstruye (`docker compose up -d --build`) y vuelve a ejecutar, guardando cada
`RESULTS.md`:

```bash
# .env: OPENAI_CHAT_MODEL=gpt-5-mini  -> ejecutar -> renombrar a RESULTS-gpt-5-mini.md
# .env: OPENAI_CHAT_MODEL=gpt-5       -> ejecutar -> renombrar a RESULTS-gpt-5.md
```

## Resultados obtenidos (gpt-5-mini, 30 casos)

Informe completo y detalle por caso en [`RESULTS.md`](RESULTS.md).

| Métrica | Resultado |
|---|---|
| Aciertos globales | **96,7%** (29/30) |
| Precisión de recuperación | **100,0%** |
| Exactitud de la respuesta | **96,0%** |
| Abstención correcta | **100,0%** |
| Latencia media / p95 | 3.928 ms / 10.523 ms |
| Coste medio por pregunta | **$0,001** (≈ $0,03 el set completo) |

### Lectura de los resultados

- **Recuperación 100%**: la búsqueda vectorial localizó el documento correcto en las 25
  preguntas respondibles. El motor de *retrieval* (embeddings + pgvector) es sólido.
- **Abstención 100%**: ninguna de las 5 preguntas fuera del corpus produjo una respuesta
  inventada. Es la evidencia de que el *grounding* funciona: **el sistema no alucina**.
- **Coste**: ~0,001 $ por consulta con `gpt-5-mini`. Proyectado a 1.000 consultas/día son
  ~30 $/mes, una cifra manejable para una operación de contact center.

### Análisis del único fallo (caso 4)

*"¿Se acumulan los datos no consumidos de un mes para el siguiente?"* → el asistente
respondió "no dispongo de esa información" cuando el dato **sí** está en el corpus.

Diagnóstico realizado sobre la base de datos: el dato está en el chunk 0 del catálogo de
tarifas y **ese chunk fue recuperado** (por eso la recuperación puntúa OK). No es un fallo
de *chunking* ni de búsqueda: el fragmento son ~1.000 caracteres dominados por la tabla de
tarifas, y la línea sobre acumulación queda diluida entre ese ruido compitiendo con otros
cuatro fragmentos. Es un fallo de **atención sobre el contexto**.

Mitigaciones candidatas (líneas futuras): *chunking* consciente de la estructura Markdown
(separar tablas de prosa), *re-ranking* de los fragmentos recuperados, o reducir `TopK`
para concentrar la atención del modelo.

### Nota metodológica

En la primera ejecución el caso 16 (*precio del NovaMesh*) apareció como fallo, pero al
revisarlo la respuesta era correcta: el precio figura en **dos** documentos y el set dorado
fijaba solo uno como fuente válida. Se corrigió el set (no el sistema), lo que subió la
precisión de recuperación del 96% al 100%. Es un recordatorio de que **el set dorado
también se depura**: un falso negativo en la medición es tan dañino como un fallo real.

## Limitaciones

- La exactitud se evalúa por **coincidencia de palabras clave**, no con un juez LLM:
  es determinista y gratis, pero puede penalizar respuestas correctas formuladas de
  otra manera. Un *LLM-as-judge* sería la evolución natural.
- El set es pequeño (30 casos) y sobre un corpus sintético controlado, adecuado para
  un MVP; en producción se ampliaría con preguntas reales de agentes.
