# ADR-009 — La campaña es una frontera obligatoria de recuperación

**Estado:** Aceptada (08/2026)

## Contexto
La Fase 8 introduce campañas: cada una agrupa su propia documentación, y el asistente de una campaña no debe poder responder con el corpus de otra (piénsese en dos clientes del contact center con argumentarios distintos y confidenciales entre sí). Un filtro *opcional* de campaña en la búsqueda convertiría un olvido del programador en una fuga de datos: el código seguiría compilando y funcionando en el caso feliz, y solo fallaría en producción cuando alguien se olvidara de pasarlo.

## Decisión
`campaignId` es parámetro **obligatorio** de `IChunkSearchService.SearchAsync`, sin ninguna sobrecarga que permita omitirlo, y `Guid.Empty` se rechaza explícitamente. El mismo requisito se propaga a `POST /chat/ask` y a la subida de documentos (`campaignId` obligatorio, sin valor por defecto). Una conversación queda ligada a su campaña para siempre: cambiar de campaña implica una conversación nueva, porque el historial se reenvía al modelo en cada turno y no se puede "limpiar" a mitad de camino.

## Consecuencias
- El aislamiento se demuestra con una única prueba: la misma pregunta, contestada en una campaña y rechazada (o vacía) en otra.
- `ChunkSearchTests.Busqueda_NuncaDevuelveFragmentosDeOtraCampaña` (SQL real) y el modo `-- isolation` del arnés de evals (API real) prueban la garantía de forma automatizada y repetible.
- Un cambio incompatible respecto a la API anterior a las campañas (v1.1.0 del contrato): deliberado, no accidental.
