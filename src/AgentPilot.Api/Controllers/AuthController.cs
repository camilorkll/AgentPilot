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
        if (result is null)
            return Unauthorized(new { code = "invalid_credentials", message = "Usuario o contraseña incorrectos." });

        return new LoginResponse(result.AccessToken, result.Role, result.ExpiresAtUtc);
    }
}
