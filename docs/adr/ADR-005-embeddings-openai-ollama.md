# ADR-005 — Embeddings: OpenAI (principal) + Ollama (alternativo local)

**Estado:** Aceptada (07/2026)

## Contexto
Anthropic no ofrece API de embeddings; OpenAI sí. En call centers los documentos pueden contener datos sensibles que no deben salir a la nube (argumento para un modo 100% local). El temario pide demostrar modelos locales.

## Decisión
- Principal: `text-embedding-3-small` (OpenAI, 1536 dims, coste ~0,02 USD/M tokens).
- Alternativo local: `nomic-embed-text` (Ollama, 768 dims), conmutable por configuración vía el puerto `IEmbeddingService`.

## Consecuencias
- Los embeddings de consulta e indexado deben generarse SIEMPRE con el mismo modelo: el modelo usado se persiste por chunk y cambiar de proveedor exige reindexar.
- El despliegue cloud no depende de Ollama.
