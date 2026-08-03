# Resultados de evaluación (evals)

- Modelo de chat: **gpt-4o-mini**
- Casos: **30** (25 respondibles, 5 fuera del corpus)
- Aciertos globales: **96,7%**

| Métrica | Resultado |
|---|---|
| Precisión de recuperación (documento correcto citado) | **100,0%** |
| Exactitud de la respuesta (dato clave presente) | **96,0%** |
| Abstención correcta (preguntas fuera del corpus) | **100,0%** |
| Fuentes en pantalla (media) | 281 ms |
| Primer token de la respuesta (media) | 1116 ms |
| Primer token p95 | 4089 ms |
| Latencia total media | 1194 ms |
| Latencia total p95 | 3354 ms |
| Coste total del set | $0,0066 |
| Coste medio por pregunta | $0,000220 |

## Detalle

| # | Pregunta | Recuperación | Respuesta | 1er token | Resultado |
|---|---|---|---|---|---|
| 1 | ¿Cuánto cuesta la tarifa Nova Mini al mes? | OK | OK | 6226 ms | **OK** |
| 2 | ¿Cuántos GB incluye la tarifa Nova Max? | OK | OK | 1454 ms | **OK** |
| 3 | ¿Qué descuento tiene la segunda línea móvil? | OK | OK | 1754 ms | **OK** |
| 4 | ¿Se acumulan los datos no consumidos de un mes para el siguiente? | OK | fallo | 4089 ms | FALLO |
| 5 | ¿Cuánto cuesta la fibra de 1 Gb simétrico? | OK | OK | 727 ms | **OK** |
| 6 | ¿Cuánto tarda la instalación estándar de fibra? | OK | OK | 779 ms | **OK** |
| 7 | ¿Qué permanencia tiene un paquete convergente? | OK | OK | 750 ms | **OK** |
| 8 | ¿Cuánto tarda en ejecutarse una portabilidad móvil? | OK | OK | 713 ms | **OK** |
| 9 | ¿Qué significa el estado APOR en una portabilidad? | OK | OK | 1119 ms | **OK** |
| 10 | ¿Hasta cuándo puede el cliente cancelar una solicitud de portabilidad? | OK | OK | 747 ms | **OK** |
| 11 | ¿Qué día del mes se emite la factura? | OK | OK | 1016 ms | **OK** |
| 12 | ¿Cuál es el importe máximo que se puede abonar por cargos de tarificación especial? | OK | OK | 1225 ms | **OK** |
| 13 | ¿En cuántos días naturales debe resolverse una reclamación? | OK | OK | 638 ms | **OK** |
| 14 | ¿Qué indica la luz LOS en rojo del router? | OK | OK | 690 ms | **OK** |
| 15 | ¿Cuánto cuesta la visita técnica si el cliente ha partido la fibra? | OK | OK | 776 ms | **OK** |
| 16 | ¿Cuánto cuesta el amplificador NovaMesh? | OK | OK | 672 ms | **OK** |
| 17 | ¿Cuántas ofertas de retención se pueden dar a un cliente al año? | OK | OK | 632 ms | **OK** |
| 18 | ¿Qué nivel de descuento requiere autorización del supervisor? | OK | OK | 667 ms | **OK** |
| 19 | ¿Cuál es el límite de datos en roaming en la UE con Nova Infinita? | OK | OK | 624 ms | **OK** |
| 20 | ¿Cuánto cuesta el Bono Viaje de 10 GB? | OK | OK | 720 ms | **OK** |
| 21 | ¿Se puede hacer un duplicado de SIM por teléfono sin código OTP? | OK | OK | 836 ms | **OK** |
| 22 | ¿Cuál es el código de la promoción de verano? | OK | OK | 790 ms | **OK** |
| 23 | ¿Cuánto se cobra si el cliente no devuelve el router tras la baja? | OK | OK | 638 ms | **OK** |
| 24 | ¿Cuántos intentos fallidos bloquean el acceso a la app? | OK | OK | 890 ms | **OK** |
| 25 | ¿Cuál es el SLA de respuesta del soporte técnico de nivel 2? | OK | OK | 700 ms | **OK** |
| 26 | ¿Cuál es el horario de atención de las tiendas físicas de TeleNova en Madrid? | — | se abstuvo | 773 ms | **OK** |
| 27 | ¿Cuántos empleados tiene TeleNova en total? | — | se abstuvo | 760 ms | **OK** |
| 28 | ¿Qué tarifas de televisión de pago ofrece TeleNova? | — | se abstuvo | 733 ms | **OK** |
| 29 | ¿Cuál es la cotización en bolsa de TeleNova? | — | se abstuvo | 652 ms | **OK** |
| 30 | ¿Ofrece TeleNova contratos de energía eléctrica? | — | se abstuvo | 692 ms | **OK** |
