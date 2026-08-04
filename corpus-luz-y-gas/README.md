# Corpus de conocimiento (sintético) — Luz y Gas Premium

Base de conocimiento de **Luz y Gas Premium**, una comercializadora de electricidad
y gas ficticia, generada con IA para desarrollo, demo y evals de AgentPilot.
Cualquier parecido con tarifas u operadoras reales es coincidencia.

10 documentos Markdown que simulan la documentación típica que consulta un agente
de atención al cliente de energía: catálogos, procedimientos, políticas y
glosario. Sigue el mismo estilo que el corpus de TeleNova (`corpus/`): ficha
operativa para agente, no folleto comercial.

## Documento largo

`03-catalogo-tarifas.md` es deliberadamente más largo (~5.500 caracteres, 7
fragmentos al indexarse) para comprobar que el sistema RAG recupera información
de fragmentos distintos, no solo del primero. Dos hechos quedan lejos del
principio del documento a propósito:

- El término fijo del ejemplo del piso de 80 m² (**11,07 €/mes**) solo aparece
  en el fragmento 3.
- La penalización del ejemplo del chalet con bomba de calor (**150 €**) solo
  aparece en los fragmentos 4-5, cerca del final. Ese mismo importe también
  aparece en `08-permanencias-y-penalizaciones.md` (es la misma promoción,
  citada desde ambos documentos): por eso el caso 2 del set dorado
  (`evals/golden-set/golden-set-luzygas.json`) no exige que se cite justo el
  catálogo, solo que la respuesta sea correcta. Un documento cruzado
  citándose desde dos sitios no es un fallo de retrieval, y hacer que la
  prueba lo exigiera habría repetido el mismo error de diseño que ya costó
  el caso del precio del NovaMesh y el código VERANO26 en el corpus de
  TeleNova.

## Subida

Como con TeleNova, estos ficheros se dejan en el repositorio y **el
administrador los sube desde la interfaz** (`/documents`, tras crear la
campaña «Luz y Gas Premium» y seleccionarla como destino). No hay ingesta
automática por script.
