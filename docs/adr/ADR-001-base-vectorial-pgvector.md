# ADR-001 — Base vectorial: pgvector

**Estado:** Aceptada (07/2026)

## Contexto
El pipeline RAG necesita almacenar embeddings y hacer búsqueda por similitud. Alternativas propuestas para la base de datos: Qdrant, Pinecone, pgvector.

## Decisión
PostgreSQL + extensión pgvector.

## Consecuencias
- Una sola base de datos para lo relacional y lo vectorial: menos piezas operativas, transacciones entre chunks y metadatos.
- Suficiente para el volumen del MVP (miles de chunks). No se espera gran cantidad de información en este desarrollo, si en un futuro se necesitaran millones, se evaluaría un motor dedicado.
