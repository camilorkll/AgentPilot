# AgentPilot 🎧🤖

> Copiloto de conocimiento **RAG** en tiempo real para agentes de Contact Center.
> Trabajo Fin de Máster — Máster en Desarrollo potenciado por IA.

Los agentes de un call center pierden entre 30 y 60 segundos por llamada buscando información en wikis, PDFs y argumentarios dispersos. **AgentPilot** indexa esa base de conocimiento y responde en lenguaje natural, en streaming y **con citas a los documentos fuente**, para que el agente resuelva sin poner la llamada en espera.

---

## 🔗 Enlaces de entrega

| Recurso | URL |
|---|---|
| 🌐 Despliegue | *(pendiente — Fase 6)* |
| 📊 Slides | *(pendiente — Fase 7)* |
| 🎬 Vídeo | *(pendiente — Fase 7)* |

## 🔑 Usuario y contraseña de prueba

| Rol | Usuario | Contraseña |
|---|---|---|
| Agente | *(pendiente — Fase 4)* | — |
| Administrador | *(pendiente — Fase 4)* | — |

---

## 🧱 Stack tecnológico

| Capa | Tecnología |
|---|---|
| Backend | .NET 8 Web API · Clean Architecture (4 capas) · EF Core |
| IA — Chat | OpenAI `gpt-5` / `gpt-5-mini` (SDK oficial .NET) con streaming |
| IA — Embeddings | OpenAI `text-embedding-3-small` · alternativo local: Ollama `nomic-embed-text` |
| IA — Orquestación | Semantic Kernel (prompt templates versionados) |
| Base de datos | PostgreSQL 16 + pgvector (relacional + vectorial) |
| Frontend | Angular 18 (standalone components + signals) |
| API | Contract-first con OpenAPI 3 ([docs/openapi.yaml](docs/openapi.yaml)) |
| Calidad | xUnit · NetArchTest · Testcontainers · tests de contrato |
| Observabilidad | Sentry · telemetría de tokens/coste por llamada LLM |
| Infraestructura | Docker Compose · GitHub Actions (CI) |

## 🚀 Instalación y ejecución

### Requisitos
- Docker Desktop
- Una API key de OpenAI

### Arranque

```bash
git clone https://github.com/camilorkll/AgentPilot.git
cd AgentPilot
cp .env.example .env        # y rellena OPENAI_API_KEY
docker compose up --build
```

- API + Swagger UI: http://localhost:8080/swagger
- Healthcheck: http://localhost:8080/api/v1/health
- Frontend: *(pendiente — Fase 4)*

### Modo embeddings 100% local (opcional)

```bash
docker compose --profile local up --build
docker exec agentpilot-ollama ollama pull nomic-embed-text
# en .env: EMBEDDINGS_PROVIDER=ollama
```

> ⚠️ El corpus debe indexarse con el mismo proveedor de embeddings con el que se consulta (ver [ADR-005](docs/adr/ADR-005-embeddings-openai-ollama.md)).

### Desarrollo sin Docker

```bash
docker compose up postgres -d
dotnet test
dotnet run --project src/AgentPilot.Api
```

## 📁 Estructura del proyecto

```
├── docs/
│   ├── openapi.yaml          # Contrato de la API (fuente de verdad, contract-first)
│   └── adr/                  # Decisiones de arquitectura (ADR-001..008)
├── src/
│   ├── AgentPilot.Domain/          # Entidades y reglas de negocio. Sin dependencias.
│   ├── AgentPilot.Application/     # Casos de uso y puertos (IChatService, IEmbeddingService)
│   ├── AgentPilot.Infrastructure/  # EF Core+pgvector, OpenAI, Ollama, Semantic Kernel
│   └── AgentPilot.Api/             # Controllers, SSE, JWT, Swagger
├── tests/
│   ├── AgentPilot.Domain.Tests/       # Unitarios de dominio puro
│   ├── AgentPilot.Application.Tests/  # Casos de uso con LLM mockeado
│   └── AgentPilot.Integration.Tests/  # Arquitectura (NetArchTest), API, Testcontainers
├── frontend/                 # Angular 18
├── evals/                    # Set dorado de preguntas + script de evaluación
├── corpus/                   # Documentos de ejemplo (sintéticos)
└── docker-compose.yml
```

Las reglas de dependencia entre capas se verifican con tests de arquitectura
([ArchitectureTests.cs](tests/AgentPilot.Integration.Tests/ArchitectureTests.cs)): `Domain` no conoce a nadie;
`Application` no conoce a `Infrastructure` ni a `Api`.

## ✨ Funcionalidades principales

- [ ] **Chat RAG con citas**: pregunta en lenguaje natural → respuesta en streaming con los fragmentos de documento usados como fuente. *(Fase 3)*
- [ ] **Ingesta de documentos**: subida de PDF/Markdown → chunking → embeddings → indexado en pgvector, en background. *(Fase 2)*
- [ ] **Búsqueda híbrida**: similitud vectorial + keyword (tsvector). *(Fase 3)*
- [ ] **Autenticación JWT** con roles agente/administrador. *(Fase 4)*
- [ ] **Feedback 👍/👎** por respuesta. *(Fase 4)*
- [ ] **Dashboard de métricas**: uso, latencia, % feedback positivo y **coste por modelo** (LLMOps). *(Fase 5)*
- [ ] **Evals**: set dorado de 25–30 preguntas con métricas de precisión de citas. *(Fase 6)*
- [x] **Proveedor de embeddings conmutable** (OpenAI cloud / Ollama local) — diseño; implementación en Fase 2.

## 🔒 Seguridad

*(Se completa en la Fase 5: OWASP Top 10, OWASP LLM Top 10 — en particular mitigación de prompt injection vía documentos ingeridos, rate limiting y validación de uploads.)*

## 🗺️ Líneas futuras

- Chat con modelo LLM local (Ollama) además de embeddings locales.
- Re-ranking del retrieval y multi-idioma.
- SSO corporativo e integración CTI/softphone.

---

*Proyecto desarrollado con asistencia de IA (Claude Code) como parte de la metodología del máster.*
