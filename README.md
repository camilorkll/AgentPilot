# AgentPilot 🎧🤖

> Copiloto de conocimiento **RAG** en tiempo real para agentes de Contact Center.
> Trabajo Fin de Máster — Máster en Desarrollo potenciado por IA.

Los agentes de un call center pierden entre 30 y 60 segundos por llamada buscando información en wikis, PDFs y argumentarios dispersos. **AgentPilot** indexa esa base de conocimiento y responde en lenguaje natural, en streaming y **con citas a los documentos fuente**, para que el agente resuelva sin poner la llamada en espera.

---

## 🔗 Enlaces de entrega

| Recurso | URL |
|---|---|
| 🌐 Despliegue | *(pendiente de publicar — guía en [docs/DEPLOY.md](docs/DEPLOY.md))* |
| 📊 Slides | *(pendiente — Fase 7)* |
| 🎬 Vídeo | *(pendiente — Fase 7)* |

## 🔑 Usuario y contraseña de prueba

Se crean automáticamente al arrancar. Autentícate en `POST /api/v1/auth/login` y
usa el token devuelto como `Authorization: Bearer <token>`.

| Rol | Usuario | Contraseña | Puede |
|---|---|---|---|
| Administrador | `admin` | `admin1234` | Todo, incluida la gestión de documentos |
| Agente | `agente` | `agente1234` | Chat RAG y consulta de documentos |

---

## 🧱 Stack tecnológico

| Capa | Tecnología |
|---|---|
| Backend | .NET 8 Web API · Clean Architecture (4 capas) · EF Core |
| IA — Chat | OpenAI `gpt-4o-mini` (SDK oficial .NET) con streaming · conmutable por configuración |
| IA — Embeddings | OpenAI `text-embedding-3-small` · alternativo local: Ollama `nomic-embed-text` |
| IA — Orquestación | Semantic Kernel (prompt templates versionados) |
| Base de datos | PostgreSQL 16 + pgvector (relacional + vectorial) |
| Frontend | Angular 20 (standalone components + signals, lazy loading) |
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

### Frontend (Angular)

```bash
cd frontend
npm install
npm start
```

- Interfaz: http://localhost:4200 (el dev-server proxya `/api` al backend del 8080)

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
├── frontend/                 # Angular 20 (standalone components + signals)
│   └── src/app/
│       ├── core/             # AuthService (signals), interceptor JWT, guardas, ApiService (SSE)
│       └── features/         # login · chat · documents · metrics (lazy loading)
├── evals/                    # Set dorado de preguntas + script de evaluación
├── corpus/                   # Documentos de ejemplo (sintéticos)
└── docker-compose.yml
```

Las reglas de dependencia entre capas se verifican con tests de arquitectura
([ArchitectureTests.cs](tests/AgentPilot.Integration.Tests/ArchitectureTests.cs)): `Domain` no conoce a nadie;
`Application` no conoce a `Infrastructure` ni a `Api`.

## ✨ Funcionalidades principales

- [x] **Ingesta de documentos**: subida de PDF/Markdown → chunking con solapamiento → embeddings → indexado en pgvector, en segundo plano. *(Fase 2 ✓)*
- [x] **Búsqueda por similitud**: recuperación de los chunks más relevantes por distancia coseno (pgvector). *(Fase 2 ✓)*
- [x] **Proveedor de embeddings conmutable** (OpenAI cloud / Ollama local) por configuración. *(Fase 2 ✓)*
- [x] **Chat RAG con citas**: pregunta en lenguaje natural → respuesta en streaming (SSE) anclada a los documentos, con citas y telemetría de coste. *(Fase 3 ✓)*
- [ ] **Búsqueda híbrida**: similitud vectorial + keyword (tsvector). *(línea futura)*
- [x] **Autenticación JWT** con roles agente/administrador (contraseñas con hash BCrypt). *(Fase 4 ✓)*
- [x] **Feedback 👍/👎** por respuesta, con comentario y autor. *(Fase 4 ✓)*
- [x] **Métricas / coste (LLMOps)**: endpoint `/metrics/summary` con uso, latencia media/p95, % feedback positivo y coste por modelo. *(Fase 5 ✓)*
- [x] **Evals**: set dorado de 30 preguntas — **100% de precisión de recuperación, 96% de exactitud y 100% de abstención correcta** (sin alucinaciones), a ~$0,001 por consulta. *(Fase 6 ✓)*

## 📏 Calidad medida (evals)

El RAG se evalúa con un **set dorado de 30 preguntas** sobre el corpus, incluidas 5 que
**no** tienen respuesta en él (para verificar que el asistente se abstiene en vez de inventar).

| Métrica | Resultado |
|---|---|
| Precisión de recuperación | **100,0%** |
| Exactitud de la respuesta | **96,0%** |
| Abstención correcta (no alucina) | **100,0%** |
| Coste medio por consulta | **~$0,001** |

Metodología, análisis del único fallo y detalle por caso en **[evals/README.md](evals/README.md)**
y [evals/RESULTS.md](evals/RESULTS.md). Reproducible con `dotnet run --project evals/AgentPilot.Evals`.

## 🔒 Seguridad

Análisis completo en **[SECURITY.md](SECURITY.md)**: mapeo a OWASP Top 10 y OWASP LLM Top 10,
gestión de secretos, y la defensa contra *prompt injection* (verificada con un documento
envenenado y con inyección directa — el asistente no obedece ninguna de las dos).
Observabilidad de errores con **Sentry** (DSN opcional por entorno).

## 🗺️ Líneas futuras

- Chat con modelo LLM local (Ollama) además de embeddings locales.
- Re-ranking del retrieval y multi-idioma.
- SSO corporativo e integración CTI/softphone.

---

*Proyecto desarrollado con asistencia de IA (Claude Code) como parte de la metodología del máster.*
