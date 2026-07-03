using System.Net.Http.Json;
using EveOnTrader.Infra.Esi.Models;

namespace EveOnTrader.Infra.Esi;

// EsiUniverseClient wraps ESI universe endpoints used by import/reference sync.
public class EsiUniverseClient
{
    private readonly EsiHttpClient _esiHttpClient;

    // Creates universe client with shared ESI HTTP wrapper.
    public EsiUniverseClient(EsiHttpClient esiHttpClient)
    {
        _esiHttpClient = esiHttpClient;
    }

    // Gets all EVE region IDs from ESI.
    public async Task<List<long>> GetRegionIdsAsync(CancellationToken cancellationToken = default)
    {
        using var resp = await _esiHttpClient.GetAsync(
            "universe/regions/?datasource=tranquility",
            "region list",
            cancellationToken);

        return await resp.Content.ReadFromJsonAsync<List<long>>(cancellationToken)
               ?? [];
    }

    // Gets all EVE solar system IDs from ESI.
    public async Task<List<long>> GetSolarSystemIdsAsync(CancellationToken cancellationToken = default)
    {
        using var resp = await _esiHttpClient.GetAsync(
            "universe/systems/?datasource=tranquility",
            "solar system list",
            cancellationToken);

        return await resp.Content.ReadFromJsonAsync<List<long>>(cancellationToken)
               ?? [];
    }

    // Resolves IDs to names/categories through ESI universe/names.
    public async Task<List<EsiUniverseName>> GetNamesAsync(
        List<long> ids,
        CancellationToken cancellationToken = default)
    {
        using var resp = await _esiHttpClient.PostAsJsonAsync(
            "universe/names/?datasource=tranquility",
            ids,
            $"universe names batch ({ids.Count:n0} ids)",
            cancellationToken);

        return await resp.Content.ReadFromJsonAsync<List<EsiUniverseName>>(cancellationToken)
               ?? [];
    }

    // Gets one solar system details row from ESI.
    public async Task<EsiSolarSystem?> GetSolarSystemAsync(
        long solarSystemId,
        CancellationToken cancellationToken = default)
    {
        using var resp = await _esiHttpClient.GetAsync(
            $"universe/systems/{solarSystemId}/?datasource=tranquility",
            $"solar system {solarSystemId}",
            cancellationToken);

        return await resp.Content.ReadFromJsonAsync<EsiSolarSystem>(cancellationToken);
    }

    // Gets one constellation details row from ESI.
    public async Task<EsiConstellation?> GetConstellationAsync(
        long constellationId,
        CancellationToken cancellationToken = default)
    {
        using var resp = await _esiHttpClient.GetAsync(
            $"universe/constellations/{constellationId}/?datasource=tranquility",
            $"constellation {constellationId}",
            cancellationToken);

        return await resp.Content.ReadFromJsonAsync<EsiConstellation>(cancellationToken);
    }

    // Tries to get one NPC station details row from ESI.
    public async Task<EsiMarketLocation?> TryGetStationAsync(
        long stationId,
        CancellationToken cancellationToken = default)
    {
        using var resp = await _esiHttpClient.TryGetAsync(
            $"universe/stations/{stationId}/?datasource=tranquility",
            $"station {stationId}",
            cancellationToken);

        if (!resp.IsSuccessStatusCode)
        {
            return null;
        }

        return await resp.Content.ReadFromJsonAsync<EsiMarketLocation>(cancellationToken);
    }

    // Gets public structure IDs visible to anonymous ESI.
    public async Task<List<long>> GetPublicStructureIdsAsync(CancellationToken cancellationToken = default)
    {
        using var resp = await _esiHttpClient.GetAsync(
            "universe/structures/?datasource=tranquility",
            "public structures list",
            cancellationToken);

        return await resp.Content.ReadFromJsonAsync<List<long>>(cancellationToken)
               ?? [];
    }

    // Gets one item type details row from ESI.
    public async Task<EsiItemType?> GetItemTypeAsync(
        long typeId,
        CancellationToken cancellationToken = default)
    {
        using var resp = await _esiHttpClient.GetAsync(
            $"universe/types/{typeId}/?datasource=tranquility",
            $"item type details {typeId}",
            cancellationToken);

        return await resp.Content.ReadFromJsonAsync<EsiItemType>(cancellationToken);
    }
}