using AgentPilot.Application.Abstractions;
using AgentPilot.Application.Campaigns;
using AgentPilot.Domain.Campaigns;

namespace AgentPilot.Application.Tests;

/// <summary>
/// CampaignService traduce "a qué estado se quiere ir" a "qué método de intención
/// llamar", y comprueba la unicidad del nombre antes de escribir. Las reglas de qué
/// transición es válida ya están probadas en el dominio (CampañaTests); aquí se prueba
/// el mapeo y el manejo de duplicados.
/// </summary>
public class CampaignServiceTests
{
    [Fact]
    public async Task CreateAsync_ConNombreDuplicado_SeRechaza()
    {
        var repo = new FakeCampaigns(new Campaña("TeleNova"));
        var service = new CampaignService(repo);

        // Sin distinguir mayúsculas: es la misma regla que el índice de la base de datos.
        await Assert.ThrowsAsync<DuplicateCampaignNameException>(
            () => service.CreateAsync("telenova", null));
    }

    [Fact]
    public async Task CreateAsync_NaceActivaYSinDocumentos()
    {
        var service = new CampaignService(new FakeCampaigns());

        var created = await service.CreateAsync("Luz y Gas Premium", "Sé breve.");

        Assert.Equal(EstadoCampaña.Activa, created.Campaign.Status);
        Assert.Equal(0, created.DocumentCount);
        Assert.Equal(0, created.ActiveDocumentCount);
    }

    [Fact]
    public async Task UpdateAsync_RenombrandoAUnNombreYaUsado_SeRechaza()
    {
        var otra = new Campaña("TeleNova");
        var repo = new FakeCampaigns(otra);
        var propia = await repo.CrearAsync("Luz y Gas Premium");
        var service = new CampaignService(repo);

        await Assert.ThrowsAsync<DuplicateCampaignNameException>(
            () => service.UpdateAsync(propia.Id, "TeleNova", null));
    }

    [Fact]
    public async Task UpdateAsync_RenombrandoseAsiMisma_NoSeConfundeConDuplicado()
    {
        // Guardar el mismo nombre (o cambiar solo las instrucciones) no debe chocar
        // contra el propio registro: por eso ExistsByNameAsync excluye su Id.
        var repo = new FakeCampaigns();
        var propia = await repo.CrearAsync("TeleNova");
        var service = new CampaignService(repo);

        var updated = await service.UpdateAsync(propia.Id, "TeleNova", "Nuevo tono.");

        Assert.Equal("Nuevo tono.", updated.Campaign.AssistantInstructions);
    }

    [Theory]
    [InlineData(EstadoCampaña.Activa, EstadoCampaña.Inactiva)]     // desactivar
    [InlineData(EstadoCampaña.Inactiva, EstadoCampaña.Activa)]     // activar
    public async Task SetStatusAsync_TransicionesSimples(EstadoCampaña desde, EstadoCampaña hacia)
    {
        var campaña = new Campaña("TeleNova");
        if (desde == EstadoCampaña.Inactiva) campaña.Desactivar();

        var service = new CampaignService(new FakeCampaigns(campaña));
        var result = await service.SetStatusAsync(campaña.Id, hacia);

        Assert.Equal(hacia, result.Campaign.Status);
    }

    [Fact]
    public async Task SetStatusAsync_AInactiva_DesdeCerrada_Reabre()
    {
        // "Inactiva" es el destino de dos transiciones distintas: hay que comprobar que
        // el servicio elige Reabrir() y no Desactivar() (que fallaría: una cerrada no
        // admite Desactivar directamente según las reglas del dominio... en realidad si
        // llamase Desactivar() sobre una cerrada, lanzaría por estar cerrada).
        var campaña = new Campaña("TeleNova");
        campaña.Desactivar();
        campaña.Cerrar();

        var service = new CampaignService(new FakeCampaigns(campaña));
        var result = await service.SetStatusAsync(campaña.Id, EstadoCampaña.Inactiva);

        Assert.Equal(EstadoCampaña.Inactiva, result.Campaign.Status);
        Assert.Null(result.Campaign.ClosedAtUtc);
    }

    [Fact]
    public async Task SetStatusAsync_TransicionImposible_PropagaElMensajeDelDominio()
    {
        var campaña = new Campaña("TeleNova"); // activa
        var service = new CampaignService(new FakeCampaigns(campaña));

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.SetStatusAsync(campaña.Id, EstadoCampaña.Cerrada));

        Assert.Contains("desactivarla", ex.Message);
    }

    [Fact]
    public async Task DeleteAsync_UnaCampañaNoCerrada_SeRechazaYNoBorraNada()
    {
        var campaña = new Campaña("TeleNova");
        var repo = new FakeCampaigns(campaña);
        var service = new CampaignService(repo);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.DeleteAsync(campaña.Id));

        Assert.False(repo.Eliminada);
    }

    [Fact]
    public async Task DeleteAsync_UnaCampañaCerrada_SeElimina()
    {
        var campaña = new Campaña("TeleNova");
        campaña.Desactivar();
        campaña.Cerrar();
        var repo = new FakeCampaigns(campaña);
        var service = new CampaignService(repo);

        await service.DeleteAsync(campaña.Id);

        Assert.True(repo.Eliminada);
    }

    [Fact]
    public async Task GetAsync_UnaCampañaQueNoExiste_LanzaKeyNotFound()
    {
        var service = new CampaignService(new FakeCampaigns());

        await Assert.ThrowsAsync<KeyNotFoundException>(() => service.GetAsync(Guid.NewGuid()));
    }

    // --- Doble en memoria ---

    private sealed class FakeCampaigns : ICampaignRepository
    {
        private readonly List<Campaña> _campañas;
        public bool Eliminada { get; private set; }

        public FakeCampaigns(params Campaña[] existentes) => _campañas = existentes.ToList();

        public async Task<Campaña> CrearAsync(string name)
        {
            var c = new Campaña(name);
            await AddAsync(c);
            return c;
        }

        public Task<Campaña?> GetByIdAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult(_campañas.FirstOrDefault(c => c.Id == id));

        public Task<IReadOnlyList<CampaignWithCounts>> ListWithCountsAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CampaignWithCounts>>(
                _campañas.Select(c => new CampaignWithCounts(c, 0, 0)).ToList());

        public Task<IReadOnlyList<CampaignWithCounts>> ListActiveWithCountsAsync(CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<CampaignWithCounts>>(
                _campañas.Where(c => c.Status == EstadoCampaña.Activa)
                    .Select(c => new CampaignWithCounts(c, 0, 0)).ToList());

        public Task<(int, int)> CountDocumentsAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult((0, 0));

        public Task<bool> ExistsByNameAsync(string name, Guid? excludingId = null, CancellationToken ct = default)
        {
            var normalizado = name.Trim().ToLowerInvariant();
            return Task.FromResult(_campañas.Any(c =>
                c.Name.ToLowerInvariant() == normalizado && c.Id != excludingId));
        }

        public Task AddAsync(Campaña c, CancellationToken ct = default) { _campañas.Add(c); return Task.CompletedTask; }
        public void Delete(Campaña c) { Eliminada = true; _campañas.Remove(c); }
        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
