using AgentPilot.Api.Contracts;
using AgentPilot.Application.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AgentPilot.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
[AllowAnonymous]
public class AuthController(IAuthService auth) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await auth.LoginAsync(request.Username, request.Password, cancellationToken);
        // CRK. Si el resultado es null o usario no existe o password incorrecta, devolvemos un 401 con un mensaje genérico, no indicamos causa.
        if (result is null)
            return Unauthorized(new { code = "invalid_credentials", message = "Usuario o contraseña incorrectos." });

        return new LoginResponse(result.AccessToken, result.Role, result.ExpiresAtUtc);
    }
}
