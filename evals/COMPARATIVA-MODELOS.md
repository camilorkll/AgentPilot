# Comparativa de modelos de chat: `gpt-5-mini` vs `gpt-4o-mini`

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
dorado de 30 casos y misma máquina. Se cambió únicamente `OpenAI__ChatModel`.
**Tres pases con cada modelo** para separar la señal del ruido: un único pase de 30
casos no distingue una diferencia real de una variación de redacción.

## Resultado

| | `gpt-5-mini` | `gpt-4o-mini` | |
|---|---|---|---|
| Aciertos (3 pases) | 29/30 · 29/30 · 29/30 | 29/30 · 29/30 · 29/30 | **empate** |
| Precisión de recuperación | 100 % | 100 % | empate |
| Exactitud de la respuesta | 96 % | 96 % | empate |
| **Abstención correcta** | 100 % | 100 % | **empate: ninguno alucina** |
| Primer token (media) | 4199 ms | **776 ms** | **5,4× más rápido** |
| Primer token (p95) | 7689 ms | **1420 ms** | 5,4× más rápido |
| Latencia total (media) | 4373 ms | **918 ms** | 4,8× más rápido |
| Coste del set de 30 | $0,0280 | **$0,0066** | **4,2× más barato** |
| Coste por pregunta | $0,000932 | **$0,000220** | 4,2× más barato |

La recuperación es idéntica por construcción: depende de los *embeddings* y de la
búsqueda vectorial, no del modelo de chat. Lo relevante es que **la abstención se
mantiene en el 100 %**: el modelo más rápido y barato tampoco inventa cuando la
respuesta no está en el corpus, que es el riesgo que de verdad importa en un
contact center.

El único fallo, idéntico en ambos y en los seis pases, es el **caso 4**
(acumulación de datos no consumidos): el dato está indexado y el fragmento correcto
*se recupera*, pero queda diluido entre ~1000 caracteres dominados por una tabla.
Es un fallo de atención sobre el contexto, no de recuperación, y por eso ningún
cambio de modelo lo arregla. Ver el análisis en [`README.md`](README.md).

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
