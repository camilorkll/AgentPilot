# ADR-007 — Modelo de chat conmutable por configuración

**Estado:** Aceptada (07/2026) · **Revisada** (03/08/2026)

## Contexto
Desarrollo y evals generan cientos de llamadas; usar el modelo de demo para todo dispara el coste.

## Decisión
El modelo de chat es configuración (`OpenAI__ChatModel`), no código: se cambia sin recompilar, y el informe de evals registra con qué modelo se obtuvo cada medida.

**Decisión original (07/2026):** `gpt-5` para demo/producción, `gpt-5-mini` para desarrollo y evals.

## Revisión (03/08/2026): el valor por defecto pasa a `gpt-4o-mini`

Al probar la aplicación desplegada, la respuesta se percibía lenta. La medición descartó la hipótesis inicial (poca información indexada): **el 95 % de la espera ocurre antes del primer token**, mientras el modelo razona, no mientras redacta ni mientras se busca en pgvector.

`gpt-5-mini` es un modelo de razonamiento: delibera antes de emitir. En RAG con el contexto ya recuperado el trabajo es sobre todo extraer y citar, no deducir, así que se midió si se pierde calidad con un modelo no razonador. Tres pases con cada uno, mismo corpus, mismo prompt y misma máquina:

| | `gpt-5-mini` | `gpt-4o-mini` |
|---|---|---|
| Aciertos (3 pases) | 29/30 · 29/30 · 29/30 | 29/30 · 29/30 · 29/30 |
| Recuperación / Exactitud / **Abstención** | 100 % / 96 % / **100 %** | 100 % / 96 % / **100 %** |
| Primer token (media) | 4199 ms | **776 ms** |
| Coste del set de 30 | $0,0280 | **$0,0066** |

Calidad indistinguible —incluido el mismo único fallo, el caso 4, que es de troceado y no de modelo—, **5,4× más rápido** y **4,2× más barato**. Método y detalle en [`../../evals/COMPARATIVA-MODELOS.md`](../../evals/COMPARATIVA-MODELOS.md).

## Consecuencias
- Coste controlado en desarrollo y ahora también en operación: ~0,0002 $ por consulta.
- En un contact center la diferencia entre 0,8 s y 4,4 s no es una cifra en una tabla: es el silencio que el agente rellena mientras el cliente espera al teléfono.
- `gpt-5-mini` sigue siendo un valor válido de configuración, preferible si el corpus crece hacia contenido que exija razonar sobre varios documentos en lugar de localizar un dato.
- La comparativa entre modelos es material de LLMOps para las slides: una decisión tomada con datos, no por intuición.
