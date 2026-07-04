# Procedimiento de escalado de incidencias y niveles de soporte

## Niveles

| Nivel | Quién | Alcance | SLA de respuesta |
|---|---|---|---|
| N1 | Agente de call center | Diagnóstico guiado, gestiones comerciales | Inmediato |
| N2 | Soporte técnico especializado | Averías no resueltas en N1, configuraciones avanzadas | 24 h laborables |
| N3 | Ingeniería de red | Incidencias de planta externa, cortes de zona | 48 h laborables |
| Supervisor | Jefe de turno | Autorizaciones especiales, clientes conflictivos, ofertas nivel 4 | En la misma llamada |

## Cuándo escalar a N2

- Diagnóstico de router completado (doc. 05) sin resolución.
- Cortes intermitentes con luces PON correctas.
- Problemas de velocidad con test por cable adjunto.
- SIEMPRE adjuntar: pasos ya realizados, resultado de tests, franja de contacto.

## Cuándo escalar a N3 (vía N2, nunca directo desde N1)

- Más de 5 incidencias abiertas en el mismo código postal en 24 h
  (posible corte de zona: consultar el mapa de incidencias antes de abrir más).

## Escalado a supervisor en llamada

- Cliente solicita expresamente hablar con un responsable (derecho reconocido:
  no negarse, tiempo máximo de espera 5 minutos o callback en 2 h).
- Amenaza de reclamación a consumo u organismos oficiales.
- Ofertas de retención de nivel 4.

## Incidencias masivas (corte de zona)

- Si el mapa muestra incidencia masiva activa: NO abrir ticket individual;
  suscribir al cliente a las notificaciones SMS de resolución y comunicar el
  tiempo estimado que figure en el aviso.
