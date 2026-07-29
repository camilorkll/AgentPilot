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

## Limitaciones

- La exactitud se evalúa por **coincidencia de palabras clave**, no con un juez LLM:
  es determinista y gratis, pero puede penalizar respuestas correctas formuladas de
  otra manera. Un *LLM-as-judge* sería la evolución natural.
- El set es pequeño (30 casos) y sobre un corpus sintético controlado, adecuado para
  un MVP; en producción se ampliaría con preguntas reales de agentes.
