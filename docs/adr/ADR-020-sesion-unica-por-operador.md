# ADR-020 — Un operador, una sesión: el último login desplaza al anterior

**Estado:** Aceptada (16/08/2026)

## Contexto

El mismo usuario podía tener abiertas tantas sesiones simultáneas como quisiera. Reproducido pidiendo dos tokens seguidos para `agente`: los dos respondían `200` a la vez.

El JWT dura ocho horas —un turno— y no había forma de revocarlo: la firma se valida sola y nadie consultaba la base de datos. Eso es lo que hace cómodo un token autocontenido, y también lo que impedía cerrar una sesión.

Tres cosas se rompen con dos sesiones a la vez, de más a menos evidente:

1. **La atribución por operador deja de significar nada.** Las métricas y el filtro de la pantalla de revisión se apoyan en `Conversation.UserName` ([ADR-015](ADR-015-valoracion-unica-por-respuesta.md) y la fase de revisión). Si dos personas usan `agente` en dos puestos, sus conversaciones se mezclan bajo el mismo nombre y ya no hay forma de separarlas — ni para medir, ni para saber quién atendió una llamada que salió mal.
2. **La señal de llamada nueva deja de funcionar.** [ADR-017](ADR-017-contexto-conversacional-acotado.md) da por terminada una llamada tras diez minutos sin preguntas, y ese corte supone **un flujo lineal de llamadas por operador**. Dos navegadores intercalan preguntas de clientes distintos: el corte por inactividad no salta cuando debe, y el contexto de un cliente puede acabar en la respuesta del siguiente. Es la fuga que ADR-017 venía a cerrar, reabierta por otra puerta.
3. **Las credenciales compartidas dejan de notarse.** Un puesto de contact center es una persona; si dos entran con el mismo usuario, nada lo delata.

## Decisión

**Una sesión abierta por usuario, y gana la última.**

`User.SessionId` se renueva en cada login y viaja en el token como claim `sid` —el nombre registrado en IANA para «Session ID», en vez de inventar uno—. En cada petición autenticada se compara el `sid` del token con el que tiene registrado el usuario; si no coinciden, `401`.

**Gana la última y no la primera**, que es la parte que se decide aquí. Rechazar el login nuevo protegería igual contra el uso simultáneo, pero dejaría fuera **hasta ocho horas** a quien cerrara el navegador sin salir o cambiara de puesto, sin nada que pudiera hacer desde su lado. En un contact center eso pasa a diario. Desplazar la sesión vieja resuelve lo mismo y es recuperable: quien fuera desplazado vuelve a entrar.

**El cliente sabe por qué.** Un `401` a secas no distingue «he caducado» de «me han desplazado», y que te echen sin explicación se lee como un fallo de la aplicación. La respuesta lleva `X-Auth-Error: session_superseded` y la pantalla de entrada lo explica.

**Un login fallido no toca la sesión abierta.** Si una contraseña equivocada la cerrara, cualquiera podría echar a un agente de su puesto sabiendo solo su nombre de usuario. Hay un test para esto.

## Consecuencias

- El token deja de ser puramente autocontenido: cada petición autenticada hace **una lectura** de `users` para comparar dos Guid. Es el precio de poder revocar, y es asumible — hay un `DbContext` por petición de todas formas, y la alternativa (una lista de revocación en memoria) no sobreviviría a un reinicio, que en Railway es cada despliegue.
- La firma, el emisor, la audiencia y la caducidad se siguen validando **antes** de esta comprobación: solo llega a consultar la base de datos un token que ya es válido.
- Los usuarios que ya existían no tienen sesión registrada (`SessionId` nulo) y sus tokens en circulación siguen valiendo hasta caducar. Se decidió así para no echar de la aplicación, al desplegar, a quien estuviera en mitad de una llamada. En cuanto vuelven a entrar quedan bajo la regla.
- **Operadores distintos no se estorban**: `agente`, `laura` y `marcos` pueden trabajar a la vez. Verificado, porque el error fácil aquí es pasarse y romper el uso normal.
- No hay cierre de sesión remoto para el administrador. Si hiciera falta, la pieza ya está: bastaría con poner `SessionId` a un valor nuevo.

## Alternativas descartadas

- **Rechazar el segundo login.** Bloquea al agente hasta ocho horas por olvidarse de salir. El daño es mayor que el que evita.
- **Lista de tokens revocados en memoria.** No sobrevive a un reinicio y, con la cola de ingesta, ya se aprendió lo que eso cuesta ([ADR-018](ADR-018-ingesta-que-falla-sin-perder-conocimiento.md)).
- **Acortar mucho la caducidad del token.** Reduce la ventana pero no impide dos sesiones a la vez, y obliga al agente a volver a entrar durante el turno.
- **Avisar sin cerrar.** Deja la decisión en manos de quien quizá esté usando credenciales ajenas.

## Referencias

- [ADR-017](ADR-017-contexto-conversacional-acotado.md) — el corte por inactividad supone un flujo lineal de llamadas por operador.
- [`SECURITY.md`](../../SECURITY.md) — autenticación y roles.
