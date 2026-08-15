# ADR-008 — Orquestación propia sobre los puertos de Application (no Semantic Kernel)

**Estado:** Aceptada (07/2026) — **Revisada** (12/08/2026): se descarta Semantic Kernel

## Contexto
Se buscaba demostrar orquestación con un framework de IA (Semantic Kernel / LangChain). La decisión original de este ADR fue adoptar Semantic Kernel como capa fina sobre los puertos de `Application`, con *prompt templates* versionados en el repo.

## Revisión: Semantic Kernel nunca llegó a integrarse
Al revisar el código para responder una pregunta sobre la diferencia entre Semantic Kernel y el SDK de OpenAI, se confirmó que la decisión original **nunca se implementó**: ningún `.csproj` del proyecto referencia `Microsoft.SemanticKernel`, y no existe ninguna plantilla de prompt al estilo SK (`skprompt.txt` / `config.json`). `OpenAiChatCompletionService` llama directamente a `OpenAI.Chat.ChatClient` del SDK oficial, y `OllamaChatCompletionService` habla la API REST nativa de Ollama por `HttpClient` — ambos detrás del puerto propio `IChatCompletionService` (`Application.Abstractions`), que **no tiene relación** con la interfaz del mismo nombre de Semantic Kernel pese a la coincidencia de nombre.

## Decisión
La orquestación es **propia**, construida con las herramientas de Clean Architecture que el proyecto ya tenía para otro fin, sin adoptar Semantic Kernel ni ningún otro framework de orquestación de terceros:

- **Conmutar de proveedor** (lo que resolvería el "conector" de SK) ya lo resuelven los puertos `IChatCompletionService` e `IEmbeddingService` de `Application`, con implementaciones intercambiables en `Infrastructure` (OpenAI / Ollama) seleccionadas por configuración (`Chat:Provider`, `Embeddings:Provider`).
- **Prompt versionado** (lo que resolverían las plantillas de SK) lo resuelve un compositor propio, `SystemPromptBuilder`, con instrucciones de campaña persistidas en `AssistantPromptSettings`/`PromptVersion` (ver [ADR-011](ADR-011-prompt-por-capas.md)) — más ajustado a la necesidad real (una instrucción de negocio por campaña, versionada y con vista previa) que una plantilla de fichero genérica.
- No hace falta *planner* ni *function calling*: el flujo RAG es fijo y conocido de antemano (recuperar → componer prompt → generar), no una secuencia de pasos que un LLM deba decidir en tiempo de ejecución. Adoptar un framework pensado para orquestar decisiones dinámicas, sobre un flujo sin ninguna decisión dinámica que orquestar, sería complejidad sin beneficio.

## Consecuencias
- Una dependencia menos en el proyecto: Semantic Kernel no aparece en ningún `.csproj`.
- La orquestación con frameworks de IA no queda cubierta por un framework de terceros, sino por el propio diseño de puertos y adaptadores de Clean Architecture — documentado aquí y en el README de forma explícita, en vez de dejar una afirmación en la documentación que el código no respalda.
- Si en el futuro el flujo dejara de ser fijo (por ejemplo, un agente que elija entre varias herramientas), Semantic Kernel o un *planner* propio volverían a ser una opción real que evaluar; hoy no aportan nada que los puertos existentes no resuelvan ya.
