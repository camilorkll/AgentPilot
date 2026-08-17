# ADR-014 — El historial de prompts es acotado y purgable

**Estado:** Aceptada (08/2026) — revisa parcialmente a [ADR-011](ADR-011-prompt-por-capas.md)

## Contexto
[ADR-011](ADR-011-prompt-por-capas.md) decidió que cada cambio de las instrucciones de campaña (incluida una restauración) añadiera una fila a un historial *append-only*, sin límite y sin borrado. Al usarlo con datos reales apareció el problema: **TeleNova acumuló 13 versiones en pocos días**, casi todas ajustes finos de una misma sesión de pruebas. El panel de administración muestra el historial completo, así que crece hasta volverse ilegible justo en la pantalla donde hay que decidir qué restaurar.

Un historial ilimitado tampoco es gratis en otro sentido: no había forma de quitar una entrada concreta. Si alguien pega por error un dato sensible o un texto equivocado en el campo libre, esa versión se queda para siempre, visible para cualquier administrador.

La alternativa —conservarlo todo y paginar la vista— resuelve la legibilidad pero no lo segundo, y añade una función de paginación a una pantalla que nadie usa para navegar histórico profundo, sino para volver a la versión de la semana pasada.

## Decisión
El historial de cada campaña conserva **como máximo `Campaña.MaxPromptVersions` entradas** (5 por defecto, configurable por campaña entre 1 y 50 desde `PUT /campaigns/{id}/prompt/max-versions`).

- Al publicar o restaurar, si con la nueva entrada se supera el límite, **se elimina la más antigua**.
- Bajar el límite purga de inmediato las que sobren.
- Un administrador puede **borrar una entrada concreta** con `DELETE /campaigns/{id}/prompt/versions/{versionId}`.
- Ambas operaciones se rechazan en una campaña `closed`, igual que el resto de cambios: cerrada es de solo lectura también para su historial.
- Lo que **no** cambia: ninguna entrada existente se reescribe, y restaurar sigue creando una entrada nueva en vez de mover la antigua ([ADR-011](ADR-011-prompt-por-capas.md)), de modo que queda constancia de que hubo una restauración, de cuándo y de quién — mientras esa entrada quepa en el límite.

El límite es **por campaña** y no global porque las campañas no se administran igual: una en rodaje cambia el prompt a diario y agradece histórico; una estable lo toca una vez al trimestre y no quiere ver ruido.

## Consecuencias
- **El historial deja de ser un registro de auditoría completo**, y esto supersede esa parte de ADR-011. La API lo dice explícitamente en `GET /prompt/versions`: devuelve lo que ha sobrevivido al límite y a los borrados, no todo lo que se publicó.
- **Riesgo aceptado y no mitigado**: un administrador puede borrar la evidencia de un cambio que hizo él mismo. Se asume porque quien puede editar el prompt ya puede cambiar el comportamiento del asistente, que es un poder mayor; el borrado no le concede nada que no tuviera. Si en el futuro hiciera falta trazabilidad real, no debe reforzarse esta tabla —es la que la interfaz edita— sino escribir un registro aparte al que la aplicación solo pueda añadir.
- El aviso de la interfaz dice cuántas entradas se llevó la purga cada vez, para que no ocurra en silencio.
- Coste de esta decisión: recuperar una versión purgada exige una copia de seguridad de la base de datos. Con el valor por defecto de 5 eso significa que, tras cinco cambios seguidos, la sexta versión hacia atrás ya no está.
