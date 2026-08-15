# ADR-003 — LLM de chat: OpenAI gpt-5 vía SDK oficial .NET

**Estado:** Aceptada (07/2026). Sustituye la elección inicial de Anthropic Claude.

## Contexto
Se necesita un LLM de calidad con streaming y buen soporte .NET. El usuario dispone de cuenta de OpenAI.

## Decisión
OpenAI `gpt-5` como modelo de demo, SDK oficial `OpenAI` (NuGet). Streaming con `CompleteChatStreamingAsync`.

## Consecuencias
- SDK oficial de .NET con streaming nativo, sin capas intermedias que mantener (ver [ADR-008](ADR-008-orquestacion-propia.md): la orquestación es propia, no Semantic Kernel).
- OpenAI ofrece también API de embeddings (ver ADR-005), simplificando el stack.
- El puerto `IChatCompletionService` en Application mantiene el proveedor como detalle intercambiable de Infrastructure.
