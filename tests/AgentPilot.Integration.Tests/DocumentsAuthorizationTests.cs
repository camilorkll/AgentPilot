using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AgentPilot.Api.Contracts;
using AgentPilot.Domain.Users;
using AgentPilot.Infrastructure.Auth;
using Microsoft.AspNetCore.Hosting;

namespace AgentPilot.Integration.Tests;

/// <summary>
/// Regresión de la asimetría registrada en SECURITY.md (A01): los tres GET de documentos
/// heredaban solo [Authorize], así que un token de agente podía leer por la API un catálogo
/// que su interfaz no le muestra (la pantalla /documents exige adminGuard). Ahora todo el
/// controlador exige rol admin y estas pruebas fijan esa frontera contra la API real:
/// el token sale del login de verdad (con su claim de sesión única) y la petición
/// atraviesa el pipeline completo de autenticación y autorización.
/// </summary>
public class DocumentsAuthorizationTests : IClassFixture<PgVectorFixture>, IClassFixture<TestApiFactory>
{
    private readonly HttpClient _client;

    // Un id cualquiera: la autorización se evalúa antes de mirar si el documento existe.
    private static readonly Guid UnDocumento = Guid.NewGuid();

    private static readonly string[] GetsDeDocumentos =
    [
        "/api/v1/documents",
        $"/api/v1/documents/{UnDocumento}",
        $"/api/v1/documents/{UnDocumento}/content",
    ];

    public DocumentsAuthorizationTests(PgVectorFixture fixture, TestApiFactory factory)
    {
        // La misma API de los demás tests de host, pero contra el Postgres real del
        // fixture: el login y la validación de sesión del token consultan usuarios.
        _client = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("ConnectionStrings:Default", fixture.ConnectionString))
            .CreateClient();

        var hasher = new BCryptPasswordHasher();
        using var db = fixture.CreateContext();
        if (!db.Users.Any(u => u.Username == "agente-authz"))
        {
            db.Users.Add(new User("agente-authz", hasher.Hash("secreto1234"), UserRole.Agent));
            db.Users.Add(new User("admin-authz", hasher.Hash("secreto1234"), UserRole.Admin));
            db.SaveChanges();
        }
    }

    private async Task<AuthenticationHeaderValue> LoginAsync(string username)
    {
        var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/login", new LoginRequest(username, "secreto1234"));
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        return new AuthenticationHeaderValue("Bearer", login!.AccessToken);
    }

    [Fact]
    public async Task LosGetDeDocumentos_ConTokenDeAgente_Responden403()
    {
        var agente = await LoginAsync("agente-authz");

        foreach (var url in GetsDeDocumentos)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = agente;

            var response = await _client.SendAsync(request);

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }
    }

    /// <summary>
    /// La contraparte: con rol admin la autorización se atraviesa y responde el
    /// controlador (200 el listado; 404 los dos GET de un documento que no existe).
    /// Sin esto, un 403 generalizado —una política mal registrada, por ejemplo—
    /// pasaría el test de arriba pareciendo la frontera de rol.
    /// </summary>
    [Fact]
    public async Task LosGetDeDocumentos_ConTokenDeAdmin_AtraviesanLaAutorizacion()
    {
        var admin = await LoginAsync("admin-authz");
        var esperados = new[] { HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.NotFound };

        for (var i = 0; i < GetsDeDocumentos.Length; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, GetsDeDocumentos[i]);
            request.Headers.Authorization = admin;

            var response = await _client.SendAsync(request);

            Assert.Equal(esperados[i], response.StatusCode);
        }
    }
}
