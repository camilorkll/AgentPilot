# ADR-015 — Una valoración por respuesta, rectificable

**Estado:** Aceptada (15/08/2026)

## Contexto
El indicador **«Respuestas útiles»** del panel de métricas se calcula como *positivos ÷ valoradas* (`MetricsRepository`). El denominador son las respuestas que alguien puntuó, no todas: es una señal de calidad sobre una muestra que se elige sola, y ya por eso hay que leerla con cuidado.

`FeedbackService.SubmitAsync` insertaba **siempre** una fila nueva, y `feedback` no tenía índice único por mensaje. Nada impedía que una misma respuesta acumulara varias valoraciones y contara varias veces en ese cociente. La interfaz ocultaba los pulgares tras votar, pero eso era estado del cliente: cualquier llamada directa a la API, o cualquier función futura que reabriera una conversación, habría bastado para duplicar.

Había además dos carencias relacionadas: no se podía rectificar una valoración (ni siquiera un clic equivocado), y el campo `Comment` existía en el modelo y en la API pero la interfaz nunca lo enviaba, así que un 👎 registraba que algo falló pero no **qué**, que es lo único que lo hace accionable.

## Decisión
**Una respuesta tiene como mucho una valoración**, y volver a valorarla la rectifica en vez de añadir otra.

- Índice único en `feedback.MessageId`, y `SubmitAsync` hace *upsert* en vez de alta. La regla la garantiza la base de datos, no solo el servicio: no depende de que todo el mundo pase por el mismo camino.
- Se puede rectificar desde el chat («cambiar» junto a la valoración emitida).
- Pasar de 👎 a 👍 **sin comentario nuevo borra el anterior**: describía un rechazo que ya no aplica, y dejarlo junto a un pulgar arriba sería una contradicción registrada.
- Pulsar 👎 abre una caja de texto opcional para el motivo. Es opcional a propósito: el agente suele estar en llamada y obligarle a escribir convertiría el voto negativo en algo que no se usa.
- `GET /conversations/{id}` devuelve la valoración de cada mensaje, en consulta aparte de la que recompone el historial — esa se ejecuta en **cada** pregunta del chat y ahí las valoraciones no pintan nada.

## Consecuencias
- El indicador de respuestas útiles no se puede inflar votando repetidamente la misma respuesta.
- **Se pierde el histórico de cambios de opinión**: si alguien vota 👎 y luego 👍, solo queda el 👍. Aceptado: interesa el juicio final del agente sobre la respuesta, no la evolución de su criterio. Quien quisiera medir eso necesitaría un registro de eventos, que es otra cosa.
- La migración **deduplica antes** de crear el índice, conservando la valoración más reciente de cada mensaje. Sin ese paso, una base con repetidos previos habría hecho fallar la migración y tumbado el despliegue.
- El motivo escrito por el agente queda guardado pero **todavía no hay ninguna pantalla que lo muestre**: es la razón de ser de la vista de administración de conversaciones valoradas negativamente. Hasta que exista, este dato se acumula sin consumirse.
