using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AgentPilot.Integration.Tests;

public class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory) => _factory = factory;

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
}
