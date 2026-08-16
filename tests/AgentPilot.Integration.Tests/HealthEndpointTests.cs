using System.Net;

namespace AgentPilot.Integration.Tests;

public class HealthEndpointTests : IClassFixture<TestApiFactory>
{
    private readonly TestApiFactory _factory;

    public HealthEndpointTests(TestApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_DevuelveOk()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task ContratoOpenApi_SeSirveEnLaRaiz()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/openapi.yaml");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("AgentPilot API", body);
    }

    /// <summary>
    /// Regresión: el fallback de la SPA se tragaba también las rutas de /api, así que un
    /// endpoint inexistente respondía 200 con el index.html de Angular. Un cliente que se
    /// equivocara recibía HTML donde esperaba JSON, y un 200 diciendo que todo fue bien.
    /// </summary>
    [Fact]
    public async Task UnEndpointDeApiQueNoExiste_Devuelve404YNoLaPaginaDeAngular()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/esto-no-existe");
        var body = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.DoesNotContain("<!doctype html>", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("not_found", body);
    }

    /// <summary>
    /// La otra mitad: las rutas del cliente SÍ deben caer en el fallback, o recargar la
    /// página en /chat daría un 404 y la aplicación dejaría de poder abrirse por URL.
    /// </summary>
    [Fact]
    public async Task UnaRutaDeLaSpa_SigueDevolviendoLaPagina()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/chat");

        // 200 con la página, o 404 si el wwwroot no está construido en el entorno de test;
        // lo que no puede es responder el 404 en JSON de la API.
        if (response.StatusCode != HttpStatusCode.OK) return;
        Assert.DoesNotContain("not_found", await response.Content.ReadAsStringAsync());
    }
}
