# ADR-006 — Acceso a datos: EF Core + pgvector-dotnet

**Estado:** Aceptada (07/2026)

## Contexto
Alternativas: Dapper (SQL manual) o EF Core.

## Decisión
EF Core con el paquete `Pgvector.EntityFrameworkCore`.

## Consecuencias
- Migraciones versionadas en el repo (requisito de reproducibilidad del despliegue).
- Soporte de tipos `vector` y operadores de distancia en LINQ; SQL crudo puntual donde convenga (búsqueda híbrida).
