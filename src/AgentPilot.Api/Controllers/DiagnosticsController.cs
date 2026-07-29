using AgentPilot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgentPilot.Api.Controllers;

/// <summary>
/// Diagnóstico del estado de la base de datos. Es anónimo a propósito: si las
/// migraciones no se han aplicado no existen usuarios con los que autenticarse,
/// justo cuando más falta hace saber qué ocurre. No expone la cadena de conexión
/// ni datos de negocio, solo el estado del esquema.
/// </summary>
[ApiController]
[Route("api/v1/health")]
[AllowAnonymous]
public class DiagnosticsController(AgentPilotDbContext db) : ControllerBase
{
    [HttpGet("database")]
    public async Task<IActionResult> Database(CancellationToken cancellationToken)
    {
        try
        {
            var canConnect = await db.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
                return Ok(new { canConnect = false, hint = "No se pudo abrir la conexión con PostgreSQL." });

            var applied = (await db.Database.GetAppliedMigrationsAsync(cancellationToken)).ToList();
            var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();

            var pgvector = await db.Database
                .SqlQuery<int>($"SELECT count(*)::int AS \"Value\" FROM pg_extension WHERE extname = 'vector'")
                .FirstOrDefaultAsync(cancellationToken);

            return Ok(new
            {
                canConnect = true,
                pgvectorInstalled = pgvector > 0,
                appliedMigrations = applied.Count,
                pendingMigrations = pending,
                hint = pgvector == 0
                    ? "Falta la extensión: ejecuta CREATE EXTENSION IF NOT EXISTS vector; en la base de datos."
                    : pending.Count > 0
                        ? "Hay migraciones pendientes: reinicia el servicio para que se apliquen."
                        : "Base de datos lista.",
            });
        }
        catch (Exception ex)
        {
            // Solo el tipo y el mensaje: suficiente para diagnosticar sin filtrar la configuración.
            return Ok(new { canConnect = false, error = ex.GetType().Name, message = ex.Message });
        }
    }
}
