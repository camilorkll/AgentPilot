using System.Text;
using AgentPilot.Application;
using AgentPilot.Application.Abstractions;
using AgentPilot.Infrastructure;
using AgentPilot.Infrastructure.Auth;
using AgentPilot.Infrastructure.Configuration;
using AgentPilot.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// --- Compatibilidad con PaaS (Railway, Render, Fly…) ---
// Muchos proveedores inyectan el puerto en PORT y la base de datos en
// DATABASE_URL con formato URI; los traducimos a lo que esperan Kestrel y Npgsql.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

var databaseUrl = Environment.GetEnvironmentVariable("DATABASE_URL");
if (!string.IsNullOrWhiteSpace(databaseUrl) && databaseUrl.StartsWith("postgres"))
{
    var uri = new Uri(databaseUrl);
    var credentials = uri.UserInfo.Split(':', 2);
    builder.Configuration["ConnectionStrings:Default"] =
        $"Host={uri.Host};Port={(uri.Port > 0 ? uri.Port : 5432)};" +
        $"Database={uri.AbsolutePath.TrimStart('/')};" +
        $"Username={credentials[0]};Password={(credentials.Length > 1 ? credentials[1] : string.Empty)};" +
        "SSL Mode=Require;Trust Server Certificate=true";
}

// --- Observabilidad: Sentry ---
// El DSN llega por configuración (Sentry:Dsn / SENTRY_DSN). Si está vacío, el
// SDK se desactiva solo: la app arranca igual, sin cuenta de Sentry.
var isDevelopment = builder.Environment.IsDevelopment();
builder.WebHost.UseSentry(options =>
{
    options.SendDefaultPii = false;                 // no enviar datos personales (privacidad/OWASP)
    options.TracesSampleRate = 0.2;                 // 20% de trazas de rendimiento
    options.MinimumEventLevel = Microsoft.Extensions.Logging.LogLevel.Error; // captura logs Error+
    options.Debug = isDevelopment;                  // en dev, registra su estado en el log
});

builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// --- Autenticación JWT ---
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
var signingKey = string.IsNullOrWhiteSpace(jwt.SigningKey)
    ? "dev-only-insecure-signing-key-please-override-me!" // fallback de arranque; en prod va por Jwt__SigningKey
    : jwt.SigningKey;

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false; // deja los claims tal cual ("role", "sub")
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            RoleClaimType = "role",
            NameClaimType = "sub",
        };
    });
builder.Services.AddAuthorization();

var app = builder.Build();

// Fuera de los tests: aplica migraciones y siembra los usuarios de prueba al
// arrancar. Se reintenta unos segundos porque la BD puede tardar en estar
// accesible (arranque del contenedor, DNS de la red).
if (!app.Environment.IsEnvironment("Testing"))
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var startupLogger = services.GetRequiredService<ILogger<Program>>();

    for (var attempt = 1; ; attempt++)
    {
        try
        {
            await services.GetRequiredService<AgentPilotDbContext>().Database.MigrateAsync();
            await IdentitySeeder.SeedAsync(
                services.GetRequiredService<IUserRepository>(),
                services.GetRequiredService<IPasswordHasher>());
            break;
        }
        catch (Exception ex) when (attempt < 10)
        {
            startupLogger.LogWarning(ex,
                "La base de datos no está lista (intento {Attempt}/10); reintento en 3 s…", attempt);
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }
}

// Contrato OpenAPI (contract-first): docs/openapi.yaml es la fuente de verdad.
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

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/api/v1/health");
app.MapControllers();

// La SPA de Angular se sirve desde wwwroot (la imagen Docker la copia ahí).
// El fallback devuelve index.html para las rutas del cliente (/chat, /metrics…),
// de modo que recargar la página no produzca un 404.
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();

// Necesario para que WebApplicationFactory (tests de integración) encuentre el entry point
public partial class Program { }
