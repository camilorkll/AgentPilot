# ADR-018 — Una ingesta que falla no puede dejar a la campaña sabiendo menos que antes

**Estado:** Aceptada (08/2026)

## Contexto

La ingesta de un documento no es atómica: extraer el texto, trocearlo, pedir los embeddings a OpenAI y guardar los fragmentos son cuatro pasos, y el tercero sale a Internet. Puede fallar por causas que no tienen nada que ver con el documento —la API caída, un timeout, la cuota agotada— y también por causas que sí —un PDF escaneado sin capa de texto, un fichero corrupto, un `.md` vacío—.

Hasta ahora, dos caminos distintos acababan en pérdida de conocimiento.

### 1. Sustituir un documento lo borraba antes de tener el sustituto

`SubmitAsync`, al recibir `replaceExisting=true`, **borraba la fila anterior y sus fragmentos** y creaba un documento nuevo que se encolaba para procesar. El borrado se confirmaba en el acto; la ingesta nueva ocurría después, en el worker, y podía fallar.

Reproducido en local, subiendo un fichero vacío sobre un documento indexado:

| | Antes de sustituir | Después del fallo |
|---|---|---|
| Estado | `Ready` | `Failed` |
| Fragmentos | 5 | **0** |
| «¿Cuál es el SLA del soporte de nivel 2?» | «…24 horas laborables [1]» | **«No dispongo de esa información»** |

El administrador quería *actualizar* una guía y la campaña se quedó **sin** esa guía. Sin vuelta atrás: los fragmentos ya no estaban y **el fichero original no se guarda en ningún sitio** —los bytes solo viajan dentro del trabajo de ingesta; de un documento se persiste el texto extraído ([ADR-012](ADR-012-texto-extraido-persistido.md)), no el fichero—. El agente que atendiera esa consulta en los minutos siguientes se encontraba un asistente que había olvidado algo que sabía.

Efecto secundario del borrado: como la fila nueva tenía otro `Id`, **las citas ya emitidas apuntaban a un documento inexistente** después de cada sustitución.

### 2. Un reinicio dejaba documentos en el limbo

La cola de ingesta vive en memoria (`System.Threading.Channels`, consumida por `IngestionBackgroundService`). Un reinicio —y en Railway **cada despliegue lo es**— pierde los trabajos encolados y el que estuviera en curso. El documento se quedaba marcado `Processing` **para siempre**: ni indexado ni fallido, fuera de las búsquedas, sin que nada volviera a intentarlo y sin nada en la pantalla que dijera que se había roto. En la práctica: se sube un documento, se despliega, y ese documento no existe para el asistente aunque en el listado parezca que "está procesándose".

## Decisión

**Ninguna de las dos situaciones puede reducir lo que la campaña sabe responder.**

**1. Sustituir es reprocesar la misma fila, no borrar y crear otra.** `SubmitAsync` reutiliza el documento existente. Sus fragmentos siguen intactos y sirviendo consultas hasta que `MarcarIndexado` los sustituya en una sola operación. Conservar el `Id` arregla además las citas colgadas.

**2. Un fallo con contenido previo devuelve el documento a `Ready`, no a `Failed`.** Si el documento ya tenía fragmentos indexados, lo que ha fallado es *la actualización*, no el documento: su contenido anterior sigue siendo válido. Se vuelve a `Ready` y **se registra el motivo** en `ErrorMessage`, para que el administrador vea que su cambio no llegó a aplicarse. Solo se queda en `Failed` cuando no hay nada que preservar —una primera ingesta que nunca produjo fragmentos—; marcarla `Ready` sería peor que el limbo, porque aparecería como indexada sin un solo fragmento.

**3. Al arrancar, un barrido saca del limbo los `Processing`.** `IngestionBackgroundService` los localiza antes de consumir la cola y les aplica la regla anterior. **No se reintenta automáticamente**: los bytes viajaban en el trabajo perdido y ya no están, y si el fallo fuera del propio documento reintentar en cada arranque sería un bucle. Se deja un estado honesto y visible para que el administrador decida.

## Consecuencias

- Una actualización fallida es un **no-op visible**, no una pérdida. El agente nunca se queda con menos de lo que tenía.
- El estado de un documento vuelve a ser informativo: `Processing` significa "ahora mismo", no "quién sabe desde cuándo".
- `Failed` queda reservado a su significado literal: *no hay nada indexado*. Un documento consultable con `ErrorMessage` puesto se lee como "sirve contenido, pero la última actualización no entró".
- **`Documento` gana un estado más que interpretar.** Se expone como `ActualizacionFallidaConContenidoAnterior` para que la UI no tenga que deducirlo de la pareja `Status`/`ErrorMessage`.
- Sigue existiendo una ventana en la que el documento sirve contenido **desactualizado** tras un fallo. Es deliberado: servir la versión anterior de una guía es mejor que no servir ninguna, y el error registrado dice que hay que reintentar.

### Lo que esto no arregla

Un reinicio **sigue perdiendo** el trabajo encolado. Esto rescata el estado, no la ingesta. La respuesta correcta sería una cola persistente fuera del proceso (Redis, RabbitMQ), y se descarta por desproporcionada: el volumen es de decenas de documentos, la ingesta la lanza una persona que está delante, y volver a subir el fichero cuesta un minuto. Lo que no era aceptable es que el fallo fuese **silencioso**.

### Una regla del dominio no puede depender de cómo se cargó la entidad

La regla del punto 2 se escribió primero mirando la colección `Chunks` en memoria. Funcionaba en los tests y en el worker —que carga el documento con `GetByIdAsync`, y ese sí trae los fragmentos— y **fallaba justo en el barrido de arranque**, que los busca con `ListAsync`, que a propósito no los carga porque serían todos sus vectores. La colección venía vacía, la regla concluía "no hay nada que preservar" y marcaba `Failed` documentos con contenido perfectamente válido: exactamente la pérdida que el barrido venía a evitar.

Se detectó **en vivo**, no en los tests, porque construir un `Documento` a mano siempre deja la colección poblada; ese estado —contador con valor, colección vacía— solo lo produce el ORM al hidratar. La regla pasó a mirar `ChunkCount`, un escalar que siempre viaja con la fila, y la regresión quedó fijada con un test de integración contra Postgres real (`DocumentRepositoryTests`) que carga por el camino que la rompía.

## Alternativas descartadas

- **Reintentar automáticamente al arrancar.** Sin los bytes no hay nada que reintentar, salvo para los reindexados. Habría que distinguir ambos casos y arriesgarse a un bucle de reintentos en cada despliegue si el documento es el problema.
- **Ingerir en una tabla aparte y hacer el cambio al final (blue/green).** Es lo correcto a gran escala, pero aquí duplica el modelo entero para resolver algo que ya resuelve reprocesar la misma fila.
- **Dejarlo en `Failed` y que el administrador reactive.** Traslada a una persona la decisión obvia —"el contenido de antes vale"— y mientras tanto la campaña responde peor.

## Referencias

- [ADR-012](ADR-012-texto-extraido-persistido.md) — de un documento se persiste el texto extraído, no el fichero: por eso perder los fragmentos era irreversible, y por eso un reindexado sí se puede reintentar.
- [ADR-008](ADR-008-orquestacion-propia.md) — la orquestación es propia, sin framework: el ciclo de vida de la ingesta y sus fallos se modelan aquí, en `Documento`.
