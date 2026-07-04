# ADR-002 — Streaming al frontend: SSE

**Estado:** Aceptada (07/2026)

## Contexto
El chat debe mostrar la respuesta del LLM token a token. Alternativas: SignalR (WebSockets), SSE, polling.

## Decisión
Server-Sent Events: el backend consume el stream del LLM y lo re-emite como eventos `token`, `citations`, `usage`, `done`.

## Consecuencias
- Unidireccional y suficiente: no necesitamos push bidireccional.
- Compatible con HTTP plano, proxies y EventSource/fetch en Angular sin dependencias extra.
