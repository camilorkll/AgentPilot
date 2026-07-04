# ADR-004 — API contract-first con OpenAPI

**Estado:** Aceptada (07/2026)

## Contexto
El temario exige Spec Driven Development. Alternativa habitual: generar Swagger desde el código (code-first).

## Decisión
`docs/openapi.yaml` se escribe a mano ANTES que el código y es la fuente de verdad. La API lo sirve tal cual en `/openapi.yaml` y Swagger UI lo renderiza. Tests de contrato validan que la implementación cumple el spec.

## Consecuencias
- El diseño de la API se revisa y discute antes de implementar.
- El frontend puede desarrollarse en paralelo contra el contrato.
