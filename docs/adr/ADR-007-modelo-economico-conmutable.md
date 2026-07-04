# ADR-007 — Modelo económico conmutable (gpt-5-mini)

**Estado:** Aceptada (07/2026)

## Contexto
Desarrollo y evals generan cientos de llamadas; usar el modelo de demo para todo dispara el coste.

## Decisión
El modelo de chat es configuración (`OpenAI__ChatModel`): `gpt-5` para demo/producción, `gpt-5-mini` para desarrollo y evals.

## Consecuencias
- Coste de desarrollo controlado (~5–10 USD en total).
- La comparativa gpt-5 vs gpt-5-mini en los evals es material de LLMOps para las slides.
