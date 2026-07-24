namespace AgentPilot.Api.Contracts;

/// <summary>Cuerpo de POST /auth/login.</summary>
public record LoginRequest(string Username, string Password);

/// <summary>Respuesta de login (esquema LoginResponse del OpenAPI).</summary>
public record LoginResponse(string AccessToken, string Role, DateTime ExpiresAtUtc);
