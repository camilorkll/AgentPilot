# ADR-010 — Los modelos (chat y embeddings) son globales, no por campaña

**Estado:** Aceptada (04/08/2026)

## Contexto
Al diseñar las campañas surgió la pregunta de si cada una debería poder elegir su propio modelo de chat o de *embeddings* (por ejemplo, una campaña más exigente con `gpt-5-mini` y otra con `gpt-4o-mini`). Se evaluó como opción y se descarta.

## Decisión
Chat y *embeddings* siguen siendo configuración **global** del sistema (`OpenAI__ChatModel`, `Embeddings__Provider`), igual que antes de que existieran las campañas. Ninguna campaña puede fijar su propio modelo.

**Motivos del descarte:**
- Los vectores de dos modelos de *embeddings* distintos no son comparables entre sí, y la columna `vector(1536)` tiene dimensión fija: mezclar modelos por campaña exigiría reindexar cada vez que una campaña cambiara de proveedor.
- Un modelo de chat por campaña obligaría a romper el puerto `IChatCompletionService` (que hoy asume un único proveedor activo) y a repetir el arnés de evals por cada combinación campaña × modelo, solo para poder seguir afirmando el 100&nbsp;% de abstención correcta que ya está medido.
- No hay degradación planeada para cuando el proveedor configurado no esté disponible: al ser global, o está configurado y funciona, o no lo está. Un selector por campaña necesitaría esa lógica de repliegue, que no aporta nada con el volumen y el corpus actuales.

## Consecuencias
- Se pierde la posibilidad de dar a una campaña un modelo más barato o más capaz que a otra. Es un ajuste fino de coste que no se justifica hoy y que puede añadirse más adelante **con datos**: `LlmCallLog` ya guarda la campaña de cada llamada, así que la información para decidirlo en el futuro está recogida desde ya.
- La tabla `campaigns` no tiene ninguna columna de modelo.
- El cambio de modelo (§ADR-007) sigue siendo una decisión medida con el arnés de evals y aplicada por variable de entorno, no una pantalla de administración: ver [`evals/COMPARATIVA-MODELOS.md`](../../evals/COMPARATIVA-MODELOS.md).
