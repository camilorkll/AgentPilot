# ---------- 1. Frontend: compila la SPA de Angular ----------
FROM node:22-alpine AS frontend
WORKDIR /app
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build -- --configuration production

# ---------- 2. Backend: compila y publica la API ----------
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY AgentPilot.sln .
COPY docs/openapi.yaml docs/
COPY src/AgentPilot.Domain/AgentPilot.Domain.csproj src/AgentPilot.Domain/
COPY src/AgentPilot.Application/AgentPilot.Application.csproj src/AgentPilot.Application/
COPY src/AgentPilot.Infrastructure/AgentPilot.Infrastructure.csproj src/AgentPilot.Infrastructure/
COPY src/AgentPilot.Api/AgentPilot.Api.csproj src/AgentPilot.Api/
RUN dotnet restore src/AgentPilot.Api/AgentPilot.Api.csproj

COPY src/ src/
RUN dotnet publish src/AgentPilot.Api/AgentPilot.Api.csproj -c Release -o /app --no-restore

# ---------- 3. Runtime: API + SPA servida como estáticos ----------
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
RUN apt-get update && apt-get install -y --no-install-recommends wget && rm -rf /var/lib/apt/lists/*

COPY --from=build /app .
# La SPA se sirve desde wwwroot: una sola URL para interfaz y API.
COPY --from=frontend /app/dist/frontend/browser ./wwwroot

EXPOSE 8080
ENTRYPOINT ["dotnet", "AgentPilot.Api.dll"]
