var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHealthChecks();

var app = builder.Build();

// Contrato OpenAPI (contract-first): docs/openapi.yaml es la fuente de verdad
// y se sirve tal cual; Swagger UI lo renderiza.
app.MapGet("/openapi.yaml", () =>
{
    var path = Path.Combine(AppContext.BaseDirectory, "openapi.yaml");
    return Results.File(path, contentType: "application/yaml");
}).ExcludeFromDescription();

app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/openapi.yaml", "AgentPilot API v1");
    options.RoutePrefix = "swagger";
});

app.MapHealthChecks("/api/v1/health");
app.MapControllers();

app.Run();

// Necesario para que WebApplicationFactory (tests de integración) encuentre el entry point
public partial class Program { }
