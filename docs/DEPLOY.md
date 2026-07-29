# Despliegue de AgentPilot

La aplicación se despliega como **una sola imagen Docker** que contiene la API .NET y
la SPA de Angular ya compilada (servida desde `wwwroot`): una única URL sirve interfaz y API.
Solo hace falta, además, un **PostgreSQL con la extensión `pgvector`**.

El proyecto está preparado para PaaS: si el proveedor inyecta `PORT` o `DATABASE_URL`
(formato URI), `Program.cs` los traduce automáticamente a lo que esperan Kestrel y Npgsql.

## Variables de entorno necesarias

| Variable | Obligatoria | Valor |
|---|---|---|
| `ConnectionStrings__Default` o `DATABASE_URL` | ✅ | Conexión a PostgreSQL con pgvector |
| `OpenAI__ApiKey` | ✅ | Clave de la API de OpenAI |
| `Jwt__SigningKey` | ✅ | Cadena aleatoria de ≥ 32 caracteres |
| `OpenAI__ChatModel` | — | `gpt-5-mini` (por defecto) o `gpt-5` |
| `Embeddings__Provider` | — | `openai` (por defecto). **No usar `ollama` en la nube.** |
| `Sentry__Dsn` | — | DSN de Sentry; vacío lo desactiva |
| `ASPNETCORE_ENVIRONMENT` | — | `Production` |

> Al arrancar, la aplicación **aplica las migraciones y crea los usuarios de prueba**
> (`admin` / `agente`), así que la base de datos queda lista sin pasos manuales.

---

## Opción elegida: Railway

### 1. Crear la cuenta
1. Entra en <https://railway.com> → **Sign up**.
2. Regístrate **con GitHub** (te permite desplegar el repo directamente y da acceso al
   crédito de prueba). Verifica la cuenta si te lo pide.

### 2. Crear el proyecto y la base de datos
1. **New Project** → **Deploy PostgreSQL**. Railway crea la BD y sus variables.
2. Abre el servicio Postgres → pestaña **Data** (o **Query**) y ejecuta:
   ```sql
   CREATE EXTENSION IF NOT EXISTS vector;
   ```
   Es el único paso manual de base de datos: sin esta extensión la migración falla.

### 3. Desplegar la aplicación
1. En el mismo proyecto: **New** → **GitHub Repo** → selecciona `camilorkll/AgentPilot`.
2. Railway detecta el Dockerfile. Si pide la ruta, indica:
   `src/AgentPilot.Api/Dockerfile` con **contexto la raíz del repositorio**.
3. En **Variables** del servicio, añade:
   - `OpenAI__ApiKey` → tu clave
   - `Jwt__SigningKey` → cadena aleatoria larga
   - `ASPNETCORE_ENVIRONMENT` → `Production`
   - `ConnectionStrings__Default` → referencia la BD del proyecto
     (Railway ofrece `${{Postgres.DATABASE_URL}}`; también puedes definir
     `DATABASE_URL` con ese valor y la app lo traduce sola).
4. **Settings → Networking → Generate Domain** para obtener la URL pública.

### 4. Verificar
```bash
curl https://TU-DOMINIO.up.railway.app/api/v1/health      # -> Healthy
```
Abre la URL en el navegador, entra con `admin` / `admin1234` y sube algún documento de
`corpus/` para poblar la base de conocimiento (el despliegue arranca sin documentos).

### 5. Anotar la URL
Añádela en la tabla de enlaces del [README](../README.md) y en el formulario de entrega.

---

## Notas y advertencias

- **Ollama no se despliega**: el modo de embeddings local es para demostración en máquina
  propia. En la nube se usa `openai` (es el valor por defecto).
- **Coste de OpenAI**: cada consulta ronda los 0,001 $ con `gpt-5-mini`. Conviene mantener
  ese modelo en el despliegue de demostración.
- **Los documentos no viajan en la imagen**: la base de conocimiento vive en la base de
  datos, así que tras el primer despliegue hay que subir los documentos desde la interfaz
  (usuario `admin`).
- **Reproducir en local la imagen de producción**:
  ```bash
  docker build -f src/AgentPilot.Api/Dockerfile -t agentpilot-full .
  docker run -p 8090:8080 --network agentpilot_default \
    -e ConnectionStrings__Default="Host=postgres;Database=agentpilot;Username=agentpilot;Password=agentpilot_dev" \
    -e OpenAI__ApiKey="$OPENAI_API_KEY" \
    -e Jwt__SigningKey="una-clave-larga-de-desarrollo-1234567890" \
    agentpilot-full
  ```
  Verificado: sirve la SPA en `/`, resuelve las rutas del cliente (p. ej. `/metrics`) y
  expone la API en `/api/v1/*`.
