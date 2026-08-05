using AgentPilot.Application.Abstractions;
using AgentPilot.Application.Campaigns;
using AgentPilot.Domain.Campaigns;

namespace AgentPilot.Application.Tests;

/// <summary>
/// CampaignService traduce "a qué estado se quiere ir" a "qué método de intención
/// llamar", y comprueba la unicidad del nombre antes de escribir. Las reglas de qué
/// transición es válida ya están probadas en el dominio (CampañaTests); aquí se prueba
/// el mapeo, el manejo de duplicados y la orquestación del historial de prompts.
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
            () => service.CreateAsync("telenova"));
    }

    [Fact]
    public async Task CreateAsync_NaceActivaSinDocumentosNiInstruccionesPropias()
    {
        var service = new CampaignService(new FakeCampaigns());

        var created = await service.CreateAsync("Luz y Gas Premium");

        Assert.Equal(EstadoCampaña.Activa, created.Campaign.Status);
        Assert.Equal(0, created.DocumentCount);
        Assert.Equal(0, created.ActiveDocumentCount);
        Assert.True(created.Campaign.AssistantPrompt.EstáVacío);
    }

    [Fact]
    public async Task UpdateAsync_RenombrandoAUnNombreYaUsado_SeRechaza()
    {
        var otra = new Campaña("TeleNova");
        var repo = new FakeCampaigns(otra);
        var propia = await repo.CrearAsync("Luz y Gas Premium");
        var service = new CampaignService(repo);

        await Assert.ThrowsAsync<DuplicateCampaignNameException>(
            () => service.UpdateAsync(propia.Id, "TeleNova"));
    }

    [Fact]
    public async Task UpdateAsync_RenombrandoseAsiMisma_NoSeConfundeConDuplicado()
    {
        // Guardar el mismo nombre no debe chocar contra el propio registro: por eso
        // ExistsByNameAsync excluye su Id.
        var repo = new FakeCampaigns();
        var propia = await repo.CrearAsync("TeleNova");
        var service = new CampaignService(repo);

        var updated = await service.UpdateAsync(propia.Id, "TeleNova");

        Assert.Equal("TeleNova", updated.Campaign.Name);
    }

    [Fact]
    public async Task GetPromptAsync_PorDefecto_EstaVacio()
    {
        var repo = new FakeCampaigns();
        var propia = await repo.CrearAsync("TeleNova");
        var service = new CampaignService(repo);

        var prompt = await service.GetPromptAsync(propia.Id);

        Assert.True(prompt.EstáVacío);
    }

    [Fact]
    public async Task UpdatePromptAsync_AplicaLosCambiosYAñadeUnaEntradaAlHistorial()
    {
        var repo = new FakeCampaigns();
        var propia = await repo.CrearAsync("TeleNova");
        var service = new CampaignService(repo);
        var settings = new AssistantPromptSettings("cercano", "breve", null, null, null);

        var result = await service.UpdatePromptAsync(propia.Id, settings, "ana");

        Assert.Same(settings, result.Settings);
        Assert.Equal("cercano", propia.AssistantPrompt.Tone);

        var historial = await service.ListPromptVersionsAsync(propia.Id);
        Assert.Single(historial);
        Assert.Equal("ana", historial[0].PublishedBy);
        Assert.Equal(result.VersionId, historial[0].Id);
    }

    [Fact]
    public async Task UpdatePromptAsync_EnCampañaCerrada_SeRechazaYNoAñadeHistorial()
    {
        var campaña = new Campaña("TeleNova");
        campaña.Desactivar();
        campaña.Cerrar();
        var repo = new FakeCampaigns(campaña);
        var service = new CampaignService(repo);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.UpdatePromptAsync(
                campaña.Id, new AssistantPromptSettings("formal", null, null, null, null), "ana"));

        Assert.Empty(await service.ListPromptVersionsAsync(campaña.Id));
    }

    [Fact]
    public async Task RestorePromptVersionAsync_VuelveAAplicarLaVersionYCreaUnaNueva()
    {
        var repo = new FakeCampaigns();
        var propia = await repo.CrearAsync("TeleNova");
        var service = new CampaignService(repo);

        var primera = await service.UpdatePromptAsync(
            propia.Id, new AssistantPromptSettings("cercano", null, null, null, null), "ana");
        await service.UpdatePromptAsync(
            propia.Id, new AssistantPromptSettings("formal", null, null, null, null), "ana");
        Assert.Equal("formal", propia.AssistantPrompt.Tone);

        var restaurada = await service.RestorePromptVersionAsync(propia.Id, primera.VersionId, "luis");

        // El tono vuelve a ser "cercano", pero la restauración es una versión nueva
        // (tres entradas en total), no una que reescriba o borre las anteriores.
        Assert.Equal("cercano", propia.AssistantPrompt.Tone);
        Assert.NotEqual(primera.VersionId, restaurada.VersionId);
        Assert.Equal(3, (await service.ListPromptVersionsAsync(propia.Id)).Count);
    }

    [Fact]
    public async Task RestorePromptVersionAsync_ConVersionDeOtraCampaña_LanzaKeyNotFound()
    {
        var repo = new FakeCampaigns();
        var campañaA = await repo.CrearAsync("TeleNova");
        var campañaB = await repo.CrearAsync("Luz y Gas Premium");
        var service = new CampaignService(repo);

        var versionDeA = await service.UpdatePromptAsync(
            campañaA.Id, new AssistantPromptSettings("cercano", null, null, null, null), "ana");

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => service.RestorePromptVersionAsync(campañaB.Id, versionDeA.VersionId, "luis"));
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
        private readonly List<PromptVersion> _versiones = [];
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

        public Task AddPromptVersionAsync(PromptVersion version, CancellationToken ct = default)
        {
            _versiones.Add(version);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<PromptVersion>> ListPromptVersionsAsync(Guid campaignId, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<PromptVersion>>(
                _versiones.Where(v => v.CampaignId == campaignId)
                    .OrderByDescending(v => v.CreatedAtUtc).ToList());

        public Task<PromptVersion?> GetPromptVersionAsync(Guid campaignId, Guid versionId, CancellationToken ct = default)
            => Task.FromResult(_versiones.FirstOrDefault(v => v.CampaignId == campaignId && v.Id == versionId));

        public Task SaveChangesAsync(CancellationToken ct = default) => Task.CompletedTask;
    }
}
