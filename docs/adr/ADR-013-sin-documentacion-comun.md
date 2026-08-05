# ADR-013 — No hay documentación común a varias campañas: se descarta

**Estado:** Aceptada (04/08/2026)

## Contexto
Si un mismo documento aplica a varias campañas (por ejemplo, una política de verificación de identidad válida para todos los clientes), la alternativa obvia es un corpus "común" que todas las campañas puedan consultar además del suyo. Se llegó a diseñar esa función (activación de documentos comunes, migración de documentos existentes a "común") antes de descartarla.

## Decisión
**No existe documentación común entre campañas.** Si un documento aplica a varias, se sube a cada una por separado. `CampaignId` sigue siendo una columna simple en `documents`, sin ningún concepto de "sin campaña = visible para todas" ni tabla de relación N a N.

**Motivo del descarte:** simplicidad y una única frontera de seguridad que se enuncia en una sola frase (ADR-009: el asistente de una campaña responde únicamente con el corpus de esa campaña, sin excepciones). Un corpus común introduce un caso especial que hay que probar, documentar y explicar aparte — y cada caso especial es una ocasión más de que alguien lo entienda mal y se filtre documentación entre campañas por accidente.

## Consecuencias
- Coste aceptado: las copias de un mismo documento en varias campañas pueden desincronizarse (si se actualiza en una, hay que recordar actualizarla en las demás). No hay mecanismo de sincronización automática.
- El modelo de datos no impide añadir en el futuro una tabla de relación si hiciera falta: la decisión de hoy no cierra la puerta, solo no lo construye sin un caso de uso real que lo justifique.
- Documentos de ejemplo que en un diseño anterior iban a marcarse como "comunes" (`08-verificacion-identidad.md`, `12-escalado-incidencias.md`) se quedan simplemente en la campaña TeleNova, como cualquier otro documento suyo.
