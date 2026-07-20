using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AgentPilot.Integration.Tests;

/// <summary>
/// Arranca la API en entorno "Testing": así se salta la auto-migración de
/// arranque (que solo corre en Development) y no requiere una BD real para
/// los tests que no la necesitan (health, contrato). Se inyecta una cadena de
/// conexión ficticia solo para que el registro del DbContext no falle; como
/// esos endpoints no tocan la BD, nunca se abre conexión.
/// </summary>
public class TestApiFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        // UseSetting se aplica pronto (config del host), antes de que Program.cs
        // lea builder.Configuration en AddInfrastructure.
        builder.UseSetting(
            "ConnectionStrings:Default",
            "Host=localhost;Database=agentpilot_test;Username=test;Password=test");
    }
}
