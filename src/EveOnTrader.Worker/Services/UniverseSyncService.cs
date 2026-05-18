using System.Net.Http.Json;
using EveOnTrader.Core.Models;
using EveOnTrader.Infra.Data;
using EveOnTrader.Worker.Models;
using Microsoft.EntityFrameworkCore;

namespace EveOnTrader.Worker.Services;

// UniverseSyncService seeds and fills typed universe reference tables needed by worker and web queries.
public class UniverseSyncService
{
    private const int BatchSize = 500;

    private readonly AppDbContext _db;
    private readonly EsiClient _esiClient;

    // Creates universe sync service with DB access and shared ESI client.
    public UniverseSyncService(AppDbContext db, EsiClient esiClient)
    {
        _db = db;
        _esiClient = esiClient;
    }

    // Syncs regions and solar systems needed for core universe reference data.
    public async Task<(int RegionsInserted, int SolarSystemsInserted)> SyncAsync()
    {
        var regionsInserted = await SyncRegionsAsync();
        var solarSystemsInserted = await SyncSolarSystemsAsync();

        return (regionsInserted, solarSystemsInserted);
    }

    // Downloads missing regions from ESI and saves their IDs and names into Regions.
    public async Task<int> SyncRegionsAsync()
    {
        Console.WriteLine("Checking regions...");

        using var regionIdsResp = await _esiClient.GetAsync(
            "universe/regions/?datasource=tranquility",
            "region list");

        var regionIds = await regionIdsResp.Content.ReadFromJsonAsync<List<long>>()
                        ?? new List<long>();

        var existingRegionIds = await _db.Regions
            .Select(x => x.RegionId)
            .ToHashSetAsync();

        var missingRegionIds = regionIds
            .Where(id => !existingRegionIds.Contains(id))
            .OrderBy(id => id)
            .ToList();

        if (missingRegionIds.Count == 0)
        {
            Console.WriteLine("No missing regions.");
            return 0;
        }

        Console.WriteLine($"Found {missingRegionIds.Count:n0} missing region IDs. Resolving names from ESI...");

        var regionsToInsert = new List<Region>();

        foreach (var batch in Batch(missingRegionIds, BatchSize))
        {
            using var resp = await _esiClient.PostAsJsonAsync(
                "universe/names/?datasource=tranquility",
                batch,
                $"region names batch ({batch.Count:n0} ids)");

            var results = await resp.Content.ReadFromJsonAsync<List<UniverseNameResult>>()
                         ?? new List<UniverseNameResult>();

            regionsToInsert.AddRange(
                results
                    .Where(x =>
                        x.Id > 0 &&
                        !string.IsNullOrWhiteSpace(x.Name) &&
                        string.Equals(x.Category, "region", StringComparison.OrdinalIgnoreCase))
                    .Select(x => new Region
                    {
                        RegionId = x.Id,
                        Name = x.Name
                    }));
        }

        if (regionsToInsert.Count == 0)
        {
            Console.WriteLine("ESI returned no new regions.");
            return 0;
        }

        _db.Regions.AddRange(regionsToInsert);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        Console.WriteLine($"Inserted {regionsToInsert.Count:n0} new regions.");
        return regionsToInsert.Count;
    }

    // Downloads missing solar systems from ESI, resolves their region IDs, and saves them into SolarSystems.
    public async Task<int> SyncSolarSystemsAsync()
    {
        Console.WriteLine("Checking solar systems...");

        using var solarSystemIdsResp = await _esiClient.GetAsync(
            "universe/systems/?datasource=tranquility",
            "solar system list");

        var solarSystemIds = await solarSystemIdsResp.Content.ReadFromJsonAsync<List<long>>()
                             ?? new List<long>();

        var existingSolarSystemIds = await _db.SolarSystems
            .Select(x => x.SolarSystemId)
            .ToHashSetAsync();

        var missingSolarSystemIds = solarSystemIds
            .Where(id => !existingSolarSystemIds.Contains(id))
            .OrderBy(id => id)
            .ToList();

        if (missingSolarSystemIds.Count == 0)
        {
            Console.WriteLine("No missing solar systems.");
            return 0;
        }

        Console.WriteLine($"Found {missingSolarSystemIds.Count:n0} missing solar system IDs. Resolving details from ESI...");

        var constellationRegionMap = new Dictionary<long, long>();
        var systemsToInsert = new List<SolarSystem>();
        var processed = 0;

        foreach (var solarSystemId in missingSolarSystemIds)
        {
            using var systemResp = await _esiClient.GetAsync(
                $"universe/systems/{solarSystemId}/?datasource=tranquility",
                $"solar system {solarSystemId}");

            var systemResult = await systemResp.Content.ReadFromJsonAsync<UniverseSolarSystemResult>();

            if (systemResult is null || string.IsNullOrWhiteSpace(systemResult.Name))
            {
                continue;
            }

            if (!constellationRegionMap.TryGetValue(systemResult.ConstellationId, out var regionId))
            {
                using var constellationResp = await _esiClient.GetAsync(
                    $"universe/constellations/{systemResult.ConstellationId}/?datasource=tranquility",
                    $"constellation {systemResult.ConstellationId}");

                var constellationResult = await constellationResp.Content.ReadFromJsonAsync<UniverseConstellationResult>();

                if (constellationResult is null)
                {
                    continue;
                }

                regionId = constellationResult.RegionId;
                constellationRegionMap[systemResult.ConstellationId] = regionId;
            }

            systemsToInsert.Add(new SolarSystem
            {
                SolarSystemId = solarSystemId,
                RegionId = regionId,
                Name = systemResult.Name,
                SecurityStatus = systemResult.SecurityStatus
            });

            processed++;

            if (processed % 100 == 0 || processed == missingSolarSystemIds.Count)
            {
                Console.WriteLine($"Resolved {processed:n0}/{missingSolarSystemIds.Count:n0} solar systems...");
            }
        }

        if (systemsToInsert.Count == 0)
        {
            Console.WriteLine("ESI returned no new solar systems.");
            return 0;
        }

        _db.SolarSystems.AddRange(systemsToInsert);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        Console.WriteLine($"Inserted {systemsToInsert.Count:n0} new solar systems.");
        return systemsToInsert.Count;
    }

    // Resolves missing market locations from imported orders and saves typed station or structure rows into MarketLocations.
    public async Task<int> SyncMarketLocationsAsync()
    {
        Console.WriteLine("Checking market locations...");

        var orderLocations = await _db.MarketOrders
            .Select(x => new { x.LocationId, x.SystemId })
            .Distinct()
            .ToListAsync();

        var existingLocationIds = await _db.MarketLocations
            .Select(x => x.LocationId)
            .ToHashSetAsync();

        var missingLocations = orderLocations
            .Where(x => x.LocationId > 0 && !existingLocationIds.Contains(x.LocationId))
            .OrderBy(x => x.LocationId)
            .ToList();

        if (missingLocations.Count == 0)
        {
            Console.WriteLine("No missing market locations.");
            return 0;
        }

        Console.WriteLine($"Found {missingLocations.Count:n0} missing market locations. Resolving details from ESI...");

        var publicStructureIdSet = new HashSet<long>();

        if (missingLocations.Any(x => !IsStationId(x.LocationId)))
        {
            using var publicStructuresResp = await _esiClient.GetAsync(
                "universe/structures/?datasource=tranquility",
                "public structures list");

            var publicStructureIds = await publicStructuresResp.Content.ReadFromJsonAsync<List<long>>()
                                     ?? new List<long>();

            publicStructureIdSet = publicStructureIds.ToHashSet();
        }

        var locationsToInsert = new List<MarketLocation>();

        foreach (var entry in missingLocations)
        {
            if (IsStationId(entry.LocationId))
            {
                using var stationResp = await _esiClient.TryGetAsync(
                    $"universe/stations/{entry.LocationId}/?datasource=tranquility",
                    $"station {entry.LocationId}");

                if (stationResp.IsSuccessStatusCode)
                {
                    var stationResult = await stationResp.Content.ReadFromJsonAsync<UniverseMarketLocationResult>();

                    if (stationResult is not null && !string.IsNullOrWhiteSpace(stationResult.Name))
                    {
                        locationsToInsert.Add(new MarketLocation
                        {
                            LocationId = entry.LocationId,
                            SolarSystemId = stationResult.SystemId,
                            Name = stationResult.Name,
                            Kind = MarketLocation.KindValue.Station,
                            HasPublicDocking = true
                        });

                        continue;
                    }
                }
            }

            locationsToInsert.Add(new MarketLocation
            {
                LocationId = entry.LocationId,
                SolarSystemId = entry.SystemId,
                Name = IsStationId(entry.LocationId)
                    ? $"(unknown station {entry.LocationId})"
                    : $"(unknown structure {entry.LocationId})",
                Kind = IsStationId(entry.LocationId)
                    ? MarketLocation.KindValue.Station
                    : MarketLocation.KindValue.UpwellStructure,
                HasPublicDocking = IsStationId(entry.LocationId)
                    ? true
                    : publicStructureIdSet.Contains(entry.LocationId)
            });
        }

        if (locationsToInsert.Count == 0)
        {
            Console.WriteLine("ESI returned no new market locations.");
            return 0;
        }

        _db.MarketLocations.AddRange(locationsToInsert);
        await _db.SaveChangesAsync();
        _db.ChangeTracker.Clear();

        Console.WriteLine($"Inserted {locationsToInsert.Count:n0} new market locations.");
        return locationsToInsert.Count;
    }

    // Returns true when location ID falls in NPC station ID range.
    private static bool IsStationId(long locationId)
    {
        return locationId >= 60_000_000 && locationId < 64_000_000;
    }

    // Splits list of IDs into fixed-size batches for batched ESI requests.
    private static IEnumerable<List<long>> Batch(List<long> source, int batchSize)
    {
        for (var i = 0; i < source.Count; i += batchSize)
        {
            yield return source.Skip(i).Take(batchSize).ToList();
        }
    }
}
