# Comparativa de modelos de chat: `gpt-5-mini`, `gpt-4o-mini` y `llama3.2:3b` local

Al probar la aplicación desplegada, la respuesta se percibía lenta. La medición
descartó la hipótesis inicial (poca información en la base de datos): el **95 % de
la espera ocurre antes del primer token**, es decir, mientras el modelo *piensa*, no
mientras redacta ni mientras se busca en pgvector.

`gpt-5-mini` es un modelo de razonamiento: delibera antes de emitir. Eso es una
ventaja en tareas de razonamiento complejo, pero en RAG con contexto ya recuperado
el trabajo es sobre todo **extraer y citar**, no deducir. La pregunta era, por
tanto, si se pierde calidad al usar un modelo no razonador.

## Método

Mismo corpus (12 documentos, 24 fragmentos), mismo prompt de sistema, mismo set
dorado de 30 casos y misma máquina para los tres modelos. Entre `gpt-5-mini` y
`gpt-4o-mini` se cambió únicamente `OpenAI__ChatModel`; para `llama3.2:3b` se cambió
`Chat__Provider=ollama` (paso 8.11), que sustituye la implementación de
`IChatCompletionService` sin tocar el resto del sistema (mismos *embeddings*, misma
recuperación, mismo prompt). **Tres pases con cada modelo** para separar la señal
del ruido: un único pase de 30 casos no distingue una diferencia real de una
variación de redacción.

**Hardware, declarado porque con un modelo local la máquina deja de ser
irrelevante**: portátil con Intel Core i7-1185G7 (4 núcleos / 8 hilos), 32 GB de RAM
y gráfica **Intel Iris Xe integrada** — sin GPU dedicada, confirmado con
`Get-CimInstance Win32_VideoController` (no hay `nvidia-smi` en el equipo). Toda la
inferencia de `llama3.2:3b` fue en **CPU**. Con OpenAI la máquina es irrelevante: la
inferencia ocurre en su nube.

`llama3.2:3b` corre en Ollama sobre el equipo anfitrión (no en un contenedor: aquí no
hay GPU que pasarle) y AgentPilot le habla por su API nativa
(`OllamaChatCompletionService`, streaming NDJSON), no por el endpoint compatible con
OpenAI, porque solo la nativa permite fijar `num_ctx` explícitamente. Es
imprescindible: el valor por defecto de Ollama (2048 tokens) es menor que el prompt
de AgentPilot (núcleo + bloque de campaña + 5 fragmentos de ~1000 caracteres +
historial, del orden de 1500-1900 tokens según el caso), y sin fijarlo el modelo
trunca el contexto **en silencio** — el síntoma sería una abstención o una respuesta
mala con las fuentes bien recuperadas en pantalla, indistinguible a simple vista de
un fallo del modelo. Se fijó `Chat__OllamaNumCtx=4096`, con margen de sobra.

## Resultado

| | `gpt-5-mini` | `gpt-4o-mini` | `llama3.2:3b` (local, CPU) | |
|---|---|---|---|---|
| Aciertos (3 pases) | 29/30 · 29/30 · 29/30 | 29/30 · 29/30 · 29/30 | 28/30 · 29/30 · 28/30 | OpenAI algo por delante |
| Precisión de recuperación | 100 % | 100 % | **100 %** | empate a tres |
| Exactitud de la respuesta | 96 % | 96 % | 92-96 % | OpenAI algo por delante |
| **Abstención correcta** | 100 % | 100 % | **100 %** | **empate: ninguno alucina** |
| Primer token (media, pases 2-3 en caliente) | 4199 ms | **776 ms** | 12979 ms | Ollama 16,7× más lento que `gpt-4o-mini` |
| Primer token (media, 3 pases sin excluir nada) | 4199 ms | 776 ms | 21226 ms¹ | — |
| Primer token (p95) | 7689 ms | 1420 ms | 43279 ms | Ollama 30,5× más lento |
| Latencia total (media, pases 2-3 en caliente) | 4373 ms | **918 ms** | 19337 ms | Ollama 21,1× más lenta |
| Coste del set de 30 | $0,0280 | $0,0066 | **$0,0000** | Ollama gratis: sin llamada a ninguna API |
| Coste por pregunta | $0,000932 | $0,000220 | **$0,000000** | — |

¹ El primer pase con Ollama incluyó la carga inicial del modelo en memoria (primer
token 37721 ms, frente a 13391 ms y 12567 ms de los pases 2 y 3 con el modelo ya
«caliente»): es coste real de un primer uso tras el arranque, no ruido de medición,
y por eso se muestra por separado en vez de excluirlo sin más. **Incluso ignorando
por completo el arranque en frío**, el modelo local sigue siendo un orden de
magnitud más lento que la nube.

La recuperación es idéntica por construcción en los tres: depende de los
*embeddings* y de la búsqueda vectorial, no del modelo de chat. Lo más destacable es
que **la abstención se mantiene en el 100 % también con el modelo local de 3.000
millones de parámetros**: no inventa más que los modelos de OpenAI cuando la
respuesta no está en el corpus, que es el riesgo que de verdad importa en un
contact center.

El único fallo compartido por los tres modelos, en los nueve pases, es el
**caso 4** (acumulación de datos no consumidos): el dato está indexado y el
fragmento correcto *se recupera*, pero queda diluido entre ~1000 caracteres
dominados por una tabla. Es un fallo de atención sobre el contexto, no de
recuperación, y por eso ningún cambio de modelo lo arregla. Ver el análisis en
[`README.md`](README.md).

`llama3.2:3b` tuvo un segundo fallo intermitente que **no aparece en ningún pase de
OpenAI**: el caso 19 (límite de datos en roaming de Nova Infinita, «25 GB/mes»)
falló en 2 de los 3 pases. La recuperación fue correcta en los tres (el fragmento de
`07-faq-roaming.md` con el dato se cita siempre); el fallo es que la respuesta no
reproduce la cifra exacta. Es coherente con lo esperable de un modelo de 3.000
millones de parámetros: retiene peor un número concreto dentro de un contexto con
varias cifras similares (el mismo fragmento menciona también «0,25 €/GB») que un
modelo mucho mayor.

También se observó más variancia dentro de cada pase con Ollama que con OpenAI (p95
muy por encima de la media incluso en los pases «calientes»): las preguntas más
lentas, en los tres pases, fueron sistemáticamente las de abstención (casos 26 y 27,
fuera del corpus) y un puñado de preguntas que piden una respuesta más elaborada
(casos 12, 13 y 24). Es coherente con generar más texto en CPU secuencialmente
—más tokens de salida, más tiempo—, aunque no se ha aislado esa causa con la misma
rigurosidad que el caso 4; queda anotado como observación, no como diagnóstico
cerrado.

## Conclusión

> **Aplicado el 03/08/2026**: `gpt-4o-mini` pasa a ser el valor por defecto del proyecto
> (`.env`, `docker-compose.yml`, `OpenAiOptions`) y de la variable `OpenAI__ChatModel` del
> despliegue. Ver [ADR-007](../docs/adr/ADR-007-modelo-economico-conmutable.md).

**`gpt-4o-mini`** para el asistente: a igualdad de calidad medida, responde en menos
de un segundo y cuesta cuatro veces menos. En un contact center la diferencia entre
0,8 s y 4,4 s no es una cifra en una tabla: es el silencio que el agente tiene que
rellenar mientras el cliente espera al teléfono.

`gpt-5-mini` se mantiene como alternativa configurable (`OpenAI__ChatModel`) porque
sigue siendo preferible si el corpus crece hacia contenido que exija razonar sobre
varios documentos en lugar de localizar un dato.

### Ollama en local: por qué no va a producción

> **Medido el 05/08/2026** (paso 8.11). Ollama se queda como herramienta de
> comparación en local, activable con `Chat__Provider=ollama`
> (`CHAT_PROVIDER=ollama` en `docker-compose.yml`); **no se despliega en Railway**.

La expectativa declarada antes de medir (§7.3 del plan de la Fase 8) era que la
inferencia en CPU tardaría «varios segundos» en el primer token. La cifra real —
**13 a 21 segundos** de media según se cuente o no el arranque en frío, con un p95
por encima de los 35 segundos— es peor que esa expectativa, no mejor. Con un cliente
al teléfono, esa espera no es viable: es la diferencia entre 0,8 s y más de diez
segundos de silencio, y aquí no hay margen de negociación posible.

Lo que sí sostiene la decisión con datos, y no solo con la intuición de que «la nube
es más rápida», son dos hallazgos:

1. **La calidad no se degrada de forma dramática.** Un modelo de 3.000 millones de
   parámetros, corriendo en un portátil sin GPU, iguala a `gpt-4o-mini` en
   recuperación y en abstención (100 % los tres), y solo pierde unos puntos en
   exactitud (92-96 % frente a 96 %). Si la prioridad fuera la privacidad —datos que
   no pueden salir del edificio— y no la latencia, `llama3.2:3b` sería una opción
   defendible, no un descarte por calidad.
2. **La latencia es la que decide, no el coste ni la calidad.** Ollama es gratis y
   privado, que es justo lo que preveía el plan; pero en un contact center la
   latencia percibida importa más que ambas cosas juntas. Esto **justifica con
   números, y no con una opinión, por qué el proyecto usa un proveedor en la nube**
   en producción.

## Limitación conocida del corrector

La puntuación de «exactitud de la respuesta» busca **texto literal** en la
respuesta, y eso midió estilo de redacción en lugar de exactitud. Dos ejemplos
reales aparecidos en estas pruebas:

- Caso 24: el modelo respondió «**Cinco** intentos fallidos» y el corrector esperaba
  «5» → fallo falso.
- Caso 9: el corpus define `APOR` como «Portada correctamente» y el modelo lo
  parafraseó de tres formas distintas en tres intentos («portada correctamente»,
  «realizado correctamente», «completado exitosamente»), todas correctas.

Correcciones aplicadas, buscando el criterio general y no la frase observada:

1. El corrector **normaliza los números escritos con letras** antes de puntuar
   («cinco» ≡ «5»). Es una regla general, no un parche para un caso.
2. En el caso 9 los datos clave codifican el **concepto** (éxito de la
   portabilidad) en lugar de una redacción concreta.

Queda dicho de forma explícita porque es una tentación metodológica real: ampliar
la lista de sinónimos cada vez que un modelo falla acaba **ajustando el examen a las
respuestas** y convierte la evaluación en un trámite que siempre aprueba. La
solución de fondo sería un corrector con modelo juez (*LLM-as-judge*) que valore
equivalencia semántica; queda anotada como trabajo futuro, con su propio coste y su
propia variabilidad.
