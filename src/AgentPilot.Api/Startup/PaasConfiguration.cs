namespace AgentPilot.Api.Startup;

/// <summary>
/// Adaptación de la configuración a las convenciones de los PaaS (Railway, Render,
/// Fly…), que suelen exponer la base de datos como una URI y el puerto en PORT.
/// </summary>
public static class PaasConfiguration
{
    /// <summary>
    /// Devuelve una cadena de conexión válida para Npgsql. 
    /// CRK La cadena puede llegar en un formato de connectionstring o si se envía en formato URL
    /// en ese caso se leen los datos y se transforma a un formato connectionstring
    /// </summary>
    public static string NormalizePostgresConnectionString(string value)
    {
        var trimmed = value.Trim();

        var isUri = trimmed.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
                 || trimmed.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase);
        if (!isUri) return trimmed;

        var uri = new Uri(trimmed);
        var credentials = uri.UserInfo.Split(':', 2);
        var user = Uri.UnescapeDataString(credentials[0]);
        var password = credentials.Length > 1 ? Uri.UnescapeDataString(credentials[1]) : string.Empty;
        var database = uri.AbsolutePath.TrimStart('/');

        // SSL Mode=Prefer: usa TLS si el servidor lo ofrece (conexiones públicas de los
        // PaaS) y texto plano si no (redes internas del propio proveedor, Postgres local).
        // Forzar Require rompería en los segundos; los certificados son del proveedor,
        // de ahí Trust Server Certificate.
        return $"Host={uri.Host};Port={(uri.Port > 0 ? uri.Port : 5432)};" +
               $"Database={database};Username={user};Password={password};" +
               "SSL Mode=Prefer;Trust Server Certificate=true";
    }
}
