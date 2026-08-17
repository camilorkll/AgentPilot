# ADR-017 — El contexto conversacional que viaja al modelo está acotado, y la llamada tiene principio

**Estado:** Aceptada (08/2026)

## Contexto
El agente trabaja horas seguidas en la misma pantalla, atendiendo a clientes distintos. Hasta ahora, cada pregunta reenviaba al modelo **la conversación entera**, sin límite. Medido sobre una conversación real (`llm_call_logs`):

| Turno | Tokens de entrada | Coste |
|---|---|---|
| 1 | 1.358 | $0,000924 |
| 2 | 1.535 | $0,001008 |
| 3 | 1.626 | $0,002255 |

Cada turno arrastra los anteriores, así que el coste por pregunta crece linealmente y el de la jornada **cuadráticamente**. Con ~150 tokens por turno, el turno 100 rondaría los 16.400 tokens de entrada.

Tres consecuencias, de más a menos grave:

1. **Rotura dura** al agotar la ventana de contexto. Con Ollama (`Chat__OllamaNumCtx` a 4.096) llega hacia el turno 18, y el truncado es **silencioso**.
2. **Coste desbocado** sin que nadie lo note: el panel muestra el total, no la curva por conversación.
3. **Peor calidad**: horas de charla ajena diluyen los fragmentos recuperados — el mismo fallo de atención que [ADR-016](ADR-016-troceado-estructural-y-reordenado.md) acababa de corregir, reintroducido por otra vía.

Y algo que no encajaba: el historial de *prompts* ya estaba acotado a 5 versiones ([ADR-014](ADR-014-historial-de-prompt-acotado.md)) por esta misma lógica, mientras que el historial de *conversación* —el que cuesta dinero en cada llamada— no tenía ningún límite.

## Decisión
**Solo viajan al modelo los 6 últimos mensajes** (3 intercambios). La conversación se sigue guardando entera: esto acota el prompt, no el registro. Métricas, revisión e histórico no cambian.

Tres intercambios bastan para la continuidad real («¿y de la otra tarifa?»); lo de hace dos horas es de otro cliente.

**La llamada tiene principio explícito.** Un botón «Nueva llamada» cierra la conversación en curso y abre otra. Como respaldo, tras **10 minutos sin preguntas** la siguiente empieza conversación nueva automáticamente, avisando.

Ese corte automático es una **simulación**: en un despliegue real la señal la daría la integración con la centralita (CTI) al colgar, sin intervención del agente. El hueco entre preguntas es la huella que deja el fin de una llamada, y aproximarlo así permite demostrar el comportamiento sin la integración.

## Consecuencias
- El coste por pregunta pasa de crecer sin fin a ser **constante**. Verificado en vivo: dos turnos encadenados dieron 1.749 y 1.647 tokens, sin acumulación.
- **La continuidad se conserva.** Verificado: «¿y cuántos GB incluye **esa**?» resolvió correctamente que hablaba de la tarifa del turno anterior.
- **Higiene entre clientes**, que es lo que de verdad justifica el botón: sin él, lo que contó un cliente sigue viajando al modelo mientras se atiende al siguiente. Verificado con la misma pregunta ambigua antes y después de pulsarlo: con contexto respondió «120 GB»; tras el corte, **se abstuvo**.
- El set dorado sigue en 30/30, aunque no ejercita este cambio: son preguntas independientes. Quien cubre el historial largo es un test de 12 turnos encadenados que comprueba que el prompt no crece.
- **Límite conocido**: el corte por inactividad es una heurística de cliente. Si el agente cierra el navegador y vuelve, la conversación anterior ya no se retoma (nunca se retomaba); y si atiende dos llamadas seguidas en menos de 10 minutos sin pulsar el botón, la segunda arrastra hasta 3 intercambios de la primera. Una integración CTI real elimina ambos casos.
- Descartado **resumir** lo antiguo con el LLM: añade una llamada por turno —coste y latencia en el camino que el agente espera— para conservar un contexto que, por lo visto, no hace falta.
- Descartado recortar por **número exacto de tokens**: exige un tokenizador por modelo y hace el comportamiento difícil de predecir. Contar turnos se explica en una frase.
