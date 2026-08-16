# Evaluación del RAG (evals)

Medición objetiva de la calidad del sistema RAG sobre el corpus de TeleNova,
en lugar de una valoración subjetiva ("parece que responde bien").

## Qué se mide

| Métrica | Qué comprueba | Por qué importa |
|---|---|---|
| **Precisión de recuperación** | El documento correcto aparece entre las citas | Mide la calidad del *retrieval* (embeddings + búsqueda vectorial) |
| **Exactitud de la respuesta** | La respuesta contiene el dato clave esperado | Mide que el modelo usa bien el contexto recuperado |
| **Abstención correcta** | Ante preguntas fuera del corpus, dice que no dispone de la información | Mide el *grounding*: que **no alucina** |
| **Tiempo hasta el primer token** | Cuánto espera el agente antes de ver la respuesta empezar | Es la espera que se percibe: con modelos de razonamiento se concentra aquí |
| Latencia total (media y p95) y coste | Rendimiento y economía por pregunta | Viabilidad operativa (LLMOps) |

## Set dorado

[`golden-set/golden-set.json`](golden-set/golden-set.json) contiene **30 casos**:

- **1–25**: preguntas con respuesta en el corpus. Cada una declara el documento
  fuente esperado y una lista de datos clave (basta que aparezca uno).
- **26–30**: preguntas **fuera del corpus** (horarios de tienda, número de empleados,
  TV de pago…). El resultado correcto es que el asistente **se abstenga**.

## Cómo ejecutarlo

Con el stack levantado (`docker compose up -d`) y el corpus ingerido:

```bash
dotnet run --project evals/AgentPilot.Evals
```

Parámetros opcionales (URL, usuario, contraseña, campaña):

```bash
dotnet run --project evals/AgentPilot.Evals -- http://localhost:8080 agente agente1234
```

El arnés autentica, lanza cada pregunta consumiendo el stream SSE dentro de una
campaña (por defecto, TeleNova), puntúa los resultados y escribe el informe en
[`RESULTS.md`](RESULTS.md). Devuelve código de salida 0 si todos los casos pasan
(útil para integrarlo en CI).

### Varias campañas en una pasada: `-- all`

```bash
dotnet run --project evals/AgentPilot.Evals -- all
```

Ejecuta cada entrada de [`golden-set/campaigns.json`](golden-set/campaigns.json)
contra su propia campaña, con una sola sesión, y escribe `RESULTS-<campaña>.md`
por cada una más un comparado en `RESULTS-CAMPAIGNS.md`. Añadir una campaña es
añadir una línea al manifiesto, sin tocar código.

**Pendiente para «Luz y Gas Premium»**: el corpus
([`corpus-luz-y-gas/`](../corpus-luz-y-gas/)) y su set dorado
([`golden-set/golden-set-luzygas.json`](golden-set/golden-set-luzygas.json), 15
casos respondibles + 5 fuera del corpus) ya están escritos, pero la campaña
todavía no existe en la aplicación: hay que crearla desde `/campaigns` y subir
los 10 documentos desde `/documents`, como con TeleNova (nadie lo hace por
script). En cuanto exista, añadir su `campaignId` real a `campaigns.json`
habilita el modo `-- all` para las dos campañas, y
`-- isolation http://localhost:8080 admin admin1234 <esaCampaignId>` prueba el
aislamiento contra su corpus real en vez de contra una campaña vacía.

### Aislamiento entre campañas: `-- isolation`

```bash
dotnet run --project evals/AgentPilot.Evals -- isolation
```

Comprobación automatizada de la garantía central de la fase de campañas: toma un
puñado de preguntas **respondibles de TeleNova ancladas a un nombre de producto**
(Nova Mini, NovaMesh, Nova Infinita, Bono Viaje — nunca un concepto genérico que
otra campaña pudiera compartir por casualidad) y las formula en **otra** campaña.
Deben abstenerse ahí, y responder con cita solo en TeleNova.

Requiere rol `admin`. Sin un cuarto argumento, crea una campaña vacía y la destruye
al terminar (incluso si alguna aserción falla); con un id de campaña real como
cuarto argumento, prueba contra ella en vez de crear una efímera — útil una vez
exista «Luz y Gas Premium», con la misma cautela sobre datos exclusivos. Escribe
[`ISOLATION-RESULTS.md`](ISOLATION-RESULTS.md) y devuelve 0 solo si no hubo fugas.

Es la contraparte automatizada, repetible y con código de salida para CI, de dos
comprobaciones ya hechas a mano en los pasos 8.3 y 8.5 (contra la API real) y de
`ChunkSearchTests.Busqueda_NuncaDevuelveFragmentosDeOtraCampaña` (contra la SQL).

## Comparar modelos

El informe registra siempre **qué modelo** lo generó (lo toma del evento `usage`, no
de la configuración), así que ningún resultado queda huérfano de su origen.

Para comparar, basta recrear el contenedor con otro modelo y volver a ejecutar:

```bash
OPENAI_CHAT_MODEL=gpt-4o-mini docker compose up -d api
```

Conviene lanzar **varios pases por modelo**: un único pase de 30 casos no distingue
una diferencia real de una variación de redacción (ver la comparativa).

Comparativa realizada entre `gpt-5-mini` y `gpt-4o-mini`, con la decisión razonada y
sus cifras: [`COMPARATIVA-MODELOS.md`](COMPARATIVA-MODELOS.md).

### Ollama en local (paso 8.11)

Desde este paso también se puede medir un modelo local con Ollama, vía un conmutador
por configuración (`Chat:Provider`, mismo patrón que `Embeddings:Provider`). Ollama
**no va a producción**: es solo una comparativa medida frente a OpenAI, no una opción de despliegue.

```bash
# Ollama corre en el equipo anfitrión (no en un contenedor), con el modelo descargado:
ollama pull llama3.2:3b

# Recrear la API apuntando a Ollama:
CHAT_PROVIDER=ollama docker compose up -d api

# Y ejecutar el arnés igual que siempre:
dotnet run --project evals/AgentPilot.Evals
```

`Chat__OllamaNumCtx` (por defecto 4096) fija la ventana de contexto explícitamente:
sin eso, la mayoría de instalaciones de Ollama usan 2048 tokens por defecto y el
prompt de AgentPilot (núcleo + bloque de campaña + 5 fragmentos + historial) se
desborda con facilidad, truncando el contexto **en silencio**.

**Medido (05/08/2026, CPU, sin GPU dedicada, 3 pases):** `llama3.2:3b` iguala a
`gpt-4o-mini` en recuperación y abstención (100 % los tres modelos) y pierde solo
unos puntos en exactitud (92-96 % frente a 96 %), pero el primer token tarda de
13 a 21 segundos según se cuente o no el arranque en frío del modelo —de 17 a 27
veces más que los 776 ms de `gpt-4o-mini`—, y gratis no es lo mismo que rápido.
Detalle completo, con el hardware y el porqué de usar la API nativa de Ollama (no
la compatible con OpenAI), en [`COMPARATIVA-MODELOS.md`](COMPARATIVA-MODELOS.md).

## Resultados obtenidos (gpt-4o-mini, 30 casos)

Informe completo y detalle por caso en [`RESULTS.md`](RESULTS.md).

| Métrica | Resultado |
|---|---|
| Aciertos globales | **100,0%** (30/30) |
| Precisión de recuperación | **100,0%** |
| Exactitud de la respuesta | **100,0%** |
| Abstención correcta | **100,0%** |
| Primer token, media | ~640 ms |
| Latencia total media | ~775 ms |
| Coste medio por pregunta | **$0,00027** (≈ $0,0080 el set completo) |

Cifras de tres pases, tras el troceado por estructura y el reordenado
([ADR-016](../docs/adr/ADR-016-troceado-estructural-y-reordenado.md)). `gpt-5-mini` daba
la misma calidad que `gpt-4o-mini` pero tardaba 4.199 ms en el primer token y costaba 4,2
veces más; la comparativa completa está en [`COMPARATIVA-MODELOS.md`](COMPARATIVA-MODELOS.md).

### Lectura de los resultados

- **Recuperación 100%**: la búsqueda localiza el documento correcto en las 25 preguntas
  respondibles.
- **Abstención 100%**: ninguna de las 5 preguntas fuera del corpus produjo una respuesta
  inventada. Es la evidencia de que el *grounding* funciona: **el sistema no alucina**.
- **Coste**: ~0,0003 $ por consulta con `gpt-4o-mini`. Proyectado a 1.000 consultas/día son
  ~8 $/mes, una cifra irrelevante frente al coste de un minuto de llamada.

### Cómo se cerró el último fallo (caso 4)

*"¿Se acumulan los datos no consumidos de un mes para el siguiente?"* falló en las tres
comparativas de modelo. El diagnóstico sobre la base de datos fue que el dato **sí** se
recuperaba, pero dentro de un fragmento de ~1.000 caracteres dominado por la tabla de
tarifas: la línea sobre acumulación quedaba diluida compitiendo con otros cuatro
fragmentos. Un fallo de **atención sobre el contexto**, no de búsqueda.

Se resolvió con las dos mitigaciones que se habían anotado como líneas futuras —troceado
consciente de la estructura y *re-ranking*—, y el set pasa entero por primera vez.

**La lección de la medición, que no era la esperada:** el troceado por estructura, aplicado
solo, **empeoró** el resultado (28/30, recuperación 96%). Al aislar cada tabla y cada
sección los fragmentos bajaron de ~1.000 a ~400 caracteres, y manteniendo `TopK` en 5 el
contexto que llegaba al modelo se redujo a menos de la mitad. Subir `TopK` a 10 y reordenar
sobre 30 candidatos es lo que convierte el cambio en una mejora. **Trocear más fino y
recuperar más piezas son la misma decisión, no dos**; medirlas por separado fue el error de
método que el arnés dejó a la vista.

### Nota metodológica

En la primera ejecución el caso 16 (*precio del NovaMesh*) apareció como fallo, pero al
revisarlo la respuesta era correcta: el precio figura en **dos** documentos y el set dorado
fijaba solo uno como fuente válida. Se corrigió el set (no el sistema), lo que subió la
precisión de recuperación del 96% al 100%. Es un recordatorio de que **el set dorado
también se depura**: un falso negativo en la medición es tan dañino como un fallo real.

El mismo patrón reapareció al comparar modelos: el corrector marcaba como fallo respuestas
correctas por escribir «Cinco» en lugar de «5», o por parafrasear un término del corpus. Se
corrigió el **criterio general** (normalizar los números escritos con letras), no cada caso
suelto, para no acabar ajustando el examen a las respuestas observadas. Detalle en
[`COMPARATIVA-MODELOS.md`](COMPARATIVA-MODELOS.md).

## Limitaciones

- La exactitud se evalúa por **coincidencia de palabras clave**, no con un juez LLM:
  es determinista y gratis, pero puede penalizar respuestas correctas formuladas de
  otra manera. Se mitiga normalizando los números escritos con letras («cinco» ≡ «5»),
  pero la paráfrasis sigue siendo su punto ciego: un *LLM-as-judge* sería la evolución
  natural.
- El set es pequeño (30 casos) y sobre un corpus sintético controlado, adecuado para
  un MVP; en producción se ampliaría con preguntas reales de agentes.
