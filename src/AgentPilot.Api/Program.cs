using AgentPilot.Infrastructure;
using AgentPilot.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

// En desarrollo, aplica las migraciones pendientes al arrancar: al hacer
// 'docker compose up' las tablas y la extensión pgvector se crean solas.
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AgentPilotDbContext>();
    db.Database.Migrate();
}

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
