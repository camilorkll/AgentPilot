using System.Text;
using AgentPilot.Api.Startup;
using AgentPilot.Application;
using AgentPilot.Infrastructure;
using AgentPilot.Infrastructure.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// --- Compatibilidad con PaaS (Railway, Render, Fly…) ---
// Muchos proveedores inyectan el puerto en PORT y la base de datos en
// DATABASE_URL con formato URI; los traducimos a lo que esperan Kestrel y Npgsql.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");

// La base de datos puede llegar como cadena Npgsql o como URI (postgresql://…),
// y en ConnectionStrings__Default o en DATABASE_URL: aceptamos las cuatro
// combinaciones y normalizamos a lo que espera Npgsql.
var rawConnectionString = builder.Configuration.GetConnectionString("Default")
    ?? Environment.GetEnvironmentVariable("DATABASE_URL");
if (!string.IsNullOrWhiteSpace(rawConnectionString))
    builder.Configuration["ConnectionStrings:Default"] =
        PaasConfiguration.NormalizePostgresConnectionString(rawConnectionString);

// Acepta también los nombres "planos" del .env / docker-compose (OPENAI_API_KEY,
// JWT_SIGNING_KEY…) como alternativa a las claves jerárquicas de .NET
// (OpenAI__ApiKey, Jwt__SigningKey…). Evita despliegues fallidos por usar unos u otros.
foreach (var (envName, configKey) in new[]
{
    ("OPENAI_API_KEY", "OpenAI:ApiKey"),
    ("OPENAI_CHAT_MODEL", "OpenAI:ChatModel"),
    ("JWT_SIGNING_KEY", "Jwt:SigningKey"),
    ("EMBEDDINGS_PROVIDER", "Embeddings:Provider"),
    ("SENTRY_DSN", "Sentry:Dsn"),
})
{
    var value = Environment.GetEnvironmentVariable(envName);
    if (!string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(builder.Configuration[configKey]))
        builder.Configuration[configKey] = value;
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

// Operador de la petición (claim 'sub' del JWT): permite atribuir la telemetría
// a cada agente sin que la capa de aplicación conozca HTTP.
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AgentPilot.Application.Abstractions.ICurrentUser, HttpCurrentUser>();

// --- Autenticación JWT ---
// Si no se ha configurado la clave de firma, generamos una aleatoria en memoria en
// lugar de usar un valor conocido (que permitiría falsificar tokens). La aplicación
// funciona, pero los tokens dejan de ser válidos al reiniciar: en un despliegue real
// hay que definir Jwt__SigningKey. La clave se escribe en la configuración para que
// el generador y el validador de tokens compartan exactamente la misma.
var signingKeyPath = $"{JwtOptions.SectionName}:SigningKey";
var configuredKey = builder.Configuration[signingKeyPath];

var generatedSigningKey = string.IsNullOrWhiteSpace(configuredKey);
var derivedShortKey = false;

if (generatedSigningKey)
{
    builder.Configuration[signingKeyPath] =
        Convert.ToBase64String(System.Security.Cryptography.RandomNumberGenerator.GetBytes(48));
}
else if (Encoding.UTF8.GetByteCount(configuredKey!) < 32)
{
    // HMAC-SHA256 exige una clave de 256 bits. Si la configurada es más corta,
    // derivamos una de 32 bytes con SHA-256: es determinista, así que los tokens
    // siguen siendo válidos entre reinicios y firma y validación coinciden.
    builder.Configuration[signingKeyPath] = Convert.ToBase64String(
        System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(configuredKey!)));
    derivedShortKey = true;
}

var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
var signingKey = jwt.SigningKey;

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

// Fuera de los tests: prepara la base de datos (migraciones + usuarios de prueba)
// en segundo plano, sin bloquear el arranque del servidor.
if (!builder.Environment.IsEnvironment("Testing"))
    builder.Services.AddHostedService<DatabaseInitializer>();

var app = builder.Build();

if (generatedSigningKey)
    app.Logger.LogWarning(
        "No se ha configurado 'Jwt__SigningKey': se ha generado una clave temporal. " +
        "Los tokens emitidos dejarán de ser válidos al reiniciar la aplicación.");
else if (derivedShortKey)
    app.Logger.LogWarning(
        "'Jwt__SigningKey' tiene menos de 32 bytes, que es el mínimo de HMAC-SHA256: " +
        "se ha derivado una clave válida a partir de ella. Configura una más larga.");

if (string.IsNullOrWhiteSpace(app.Configuration["OpenAI:ApiKey"]))
    app.Logger.LogWarning(
        "No se ha configurado 'OpenAI__ApiKey': la ingesta de documentos y el chat fallarán.");

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
