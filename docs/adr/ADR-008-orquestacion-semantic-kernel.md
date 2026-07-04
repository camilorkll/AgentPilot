# ADR-008 — Orquestación: Semantic Kernel

**Estado:** Aceptada (07/2026)

## Contexto
El temario pide orquestación con frameworks de IA (Semantic Kernel / LangChain). OpenAI es el conector de primera clase de SK en .NET.

## Decisión
Semantic Kernel como capa fina de orquestación: prompt templates versionados en el repo sobre los puertos de Application. Sin agentes complejos en el MVP.

## Consecuencias
- Los prompts dejan de ser strings embebidos: se versionan y se revisan en PRs.
- La capa es opcional para el funcionamiento (los puertos la aíslan); si estorbara, se retira sin tocar Application.
