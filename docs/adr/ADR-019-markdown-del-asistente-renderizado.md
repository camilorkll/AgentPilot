# ADR-019 — El formato del asistente se renderiza con un subconjunto propio, no con una librería

**Estado:** Aceptada (16/08/2026)

## Contexto

El modelo responde en Markdown por defecto —el núcleo del prompt ([ADR-011](ADR-011-prompt-por-capas.md)) no dice nada del formato— y la interfaz lo pintaba como texto plano. El agente leía esto, literalmente:

```
1. **Revisión de tarifa** (downgrade a tarifa más ajustada) - aplica a cualquier cliente.
2. **20% descuento en fibra durante 6 meses** - para clientes con antigüedad mayor a 12 meses.
```

Los asteriscos a la vista, y sin jerarquía visual. Se notaba justo en las respuestas más útiles —argumentarios, procedimientos por pasos, condiciones con excepciones—, que son las que llevan estructura; las respuestas de una sola frase no lo delataban, y por eso pasó desapercibido hasta que se revisó la aplicación pantalla por pantalla. En una llamada real, el agente lee en diagonal mientras habla: esa estructura es exactamente lo que le hace falta.

Había dos salidas: **decirle al modelo que no use Markdown**, o **renderizarlo**.

## Decisión

**Se renderiza, con un renderizador propio de unas 60 líneas** (`frontend/src/app/core/markdown.ts`), no con una librería.

Quitar el formato desde el prompt era más barato y eliminaba el problema de raíz, pero renuncia a algo que ayuda al agente durante la llamada, y además toca el núcleo del prompt — que es la pieza que [ADR-011](ADR-011-prompt-por-capas.md) protege de cambios oportunistas y que obligaría a repasar los evals.

Lo delicado es de dónde viene ese texto: **lo escribe un LLM que acaba de leer documentos de campaña**. Es contenido no confiable por definición, y el propio corpus incluye un `prompt-injection-test.md` para probar precisamente eso. Convertirlo en HTML es abrirle un camino al DOM.

Un generador de Markdown de propósito general emite HTML arbitrario —enlaces, imágenes, atributos— y obliga a confiar en su saneado y en el de sus dependencias. Aquí el conjunto de etiquetas que pueden producirse es cerrado y cabe en una línea: `<p>`, `<br>`, `<ul>`, `<ol>`, `<li>`, `<strong>`, `<em>` y `<code>`.

La seguridad se apoya en tres capas, en este orden:

1. **Se escapa primero.** `&`, `<`, `>` y `"` se neutralizan **antes** de transformar nada. A partir de ahí el contenido no puede producir una etiqueta: las que aparecen las pone el renderizador.
2. **No se generan enlaces ni imágenes.** El asistente cita con `[1]`, no con URLs, así que no hace falta emitir `<a>` ni `<img>`: la superficie de `javascript:` y de `onerror=` desaparece entera en vez de tener que filtrarse. Es la ventaja de un subconjunto elegido y no heredado.
3. **Angular vuelve a sanear.** El binding `[innerHTML]` pasa por `DomSanitizer`.

**Solo se renderiza lo que escribe el asistente.** Lo que teclea el operador se sigue pintando como texto: no necesita formato, y darle un camino a HTML sería regalar superficie a cambio de nada.

## Consecuencias

- El agente lee listas y negritas como tales, en el chat y en la pantalla de revisión.
- Fuera de alcance a propósito: **tablas, encabezados, citas en bloque y enlaces**. El asistente no los usa al responder —las tablas del corpus llegan como dato dentro de los fragmentos recuperados, no en la respuesta— y cada uno añadiría casos que probar sin mejorar lo que se lee durante una llamada. Si alguna vez aparecen, se degradan a texto visible, que es lo que ya pasaba con todo.
- Se renderiza también mientras la respuesta se escribe. Las marcas sin cerrar (`**Revisión` antes de su segundo `**`) se quedan literales hasta que cierran, como en cualquier chat.
- Hay 12 tests sobre el renderizador, la mitad de seguridad. Son los primeros tests unitarios de frontend del proyecto.

### Lo que salió al probarlo

Dos cosas, y conviene distinguirlas porque no son iguales:

- **Un fallo real.** El primer intento convertía los fragmentos de código a `<code>` antes de aplicar negritas, con el comentario de que así su contenido no se reinterpretaba. Es falso: ir primero no protege de las pasadas siguientes, que seguían viendo los asteriscos dentro de la etiqueta. `` `**esto**` `` salía en negrita en vez de literal. Ahora la línea se **parte** por los fragmentos de código y solo se transforma lo de fuera. Lo detectó un test, no la lectura del código.
- **Un test equivocado.** Otro exigía que la cadena `onerror=alert` no apareciera en la salida. Aparece —como texto escapado y visible, `&lt;img src=x onerror=alert(1)&gt;`—, y eso es inofensivo: lo que importa es que no haya etiqueta. El test daba por malo un resultado correcto y se corrigió el test, no el código.

## Referencias

- [ADR-011](ADR-011-prompt-por-capas.md) — el núcleo del prompt, que se decidió no tocar para resolver esto.
- [`SECURITY.md`](../../SECURITY.md) — tratamiento del contenido del corpus como no confiable.
