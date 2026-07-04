# ADR-001 — Base vectorial: pgvector

**Estado:** Aceptada (07/2026)

## Contexto
El pipeline RAG necesita almacenar embeddings y hacer búsqueda por similitud. Alternativas: Qdrant, Pinecone, pgvector.

## Decisión
PostgreSQL + extensión pgvector.

## Consecuencias
- Una sola base de datos para lo relacional y lo vectorial: menos piezas operativas, transacciones entre chunks y metadatos.
- Suficiente para el volumen del MVP (miles de chunks). Si escalara a millones, se evaluaría un motor dedicado.
