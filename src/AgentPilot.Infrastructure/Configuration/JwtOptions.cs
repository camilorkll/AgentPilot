namespace AgentPilot.Infrastructure.Configuration;

/// <summary>Sección "Jwt" de la configuración.</summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "AgentPilot";
    public string Audience { get; set; } = "AgentPilot";

    /// <summary>Clave de firma HMAC (mín. 32 bytes). Nunca en el repo: por variable de entorno.</summary>
    public string SigningKey { get; set; } = string.Empty;

    public int ExpiryMinutes { get; set; } = 480; // 8 horas (un turno)
}
