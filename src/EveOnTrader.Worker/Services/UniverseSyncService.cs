using System.Net.Http.Headers;
using System.Net.Http.Json;
using EveOnTrader.Core.Models;
using EveOnTrader.Infra.Data;
using EveOnTrader.Worker.Models;
using Microsoft.EntityFrameworkCore;

namespace EveOnTrader.Worker.Services;

public class UniverseSyncService
{
    private const string CompatibilityDate = "2025-12-16";
    private const int BatchSize = 500;

    private readonly AppDbContext _db;

    public UniverseSyncService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<(int RegionsInserted, int SolarSystemsInserted)> SyncAsync()
    {
        var regionsInserted = await SyncRegionsAsync();
        var solarSystemsInserted = await SyncSolarSystemsAsync();

        return (regionsInserted, solarSystemsInserted);
    }

    public async Task<int> SyncRegionsAsync()
    {
        Console.WriteLine("Checking regions...");

        using var http = CreateHttpClient();

        var regionIds = await http.GetFromJsonAsync<List<long>>(
                            "universe/regions/?datasource=tranquility")
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
            using var resp = await http.PostAsJsonAsync(
                "universe/names/?datasource=tranquility",
                batch);

            resp.EnsureSuccessStatusCode();

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

    public async Task<int> SyncSolarSystemsAsync()
    {
        Console.WriteLine("Checking solar systems...");

        using var http = CreateHttpClient();

        var solarSystemIds = await http.GetFromJsonAsync<List<long>>(
                                 "universe/systems/?datasource=tranquility")
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
            using var systemResp = await http.GetAsync(
                $"universe/systems/{solarSystemId}/?datasource=tranquility");

            systemResp.EnsureSuccessStatusCode();

            var systemResult = await systemResp.Content.ReadFromJsonAsync<UniverseSolarSystemResult>();

            if (systemResult is null || string.IsNullOrWhiteSpace(systemResult.Name))
            {
                continue;
            }

            if (!constellationRegionMap.TryGetValue(systemResult.ConstellationId, out var regionId))
            {
                using var constellationResp = await http.GetAsync(
                    $"universe/constellations/{systemResult.ConstellationId}/?datasource=tranquility");

                constellationResp.EnsureSuccessStatusCode();

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

    public async Task<int> SyncMarketLocationsAsync()
    {
        Console.WriteLine("Checking market locations...");

        using var http = CreateHttpClient();

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
            var publicStructureIds = await http.GetFromJsonAsync<List<long>>(
                                         "universe/structures/?datasource=tranquility")
                                     ?? new List<long>();

            publicStructureIdSet = publicStructureIds.ToHashSet();
        }

        var locationsToInsert = new List<MarketLocation>();

        foreach (var entry in missingLocations)
        {
            if (IsStationId(entry.LocationId))
            {
                using var stationResp = await http.GetAsync(
                    $"universe/stations/{entry.LocationId}/?datasource=tranquility");

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

    private static bool IsStationId(long locationId)
    {
        return locationId >= 60_000_000 && locationId < 64_000_000;
    }


    private static HttpClient CreateHttpClient()
    {
        var http = new HttpClient
        {
            BaseAddress = new Uri("https://esi.evetech.net/latest/")
        };

        http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        http.DefaultRequestHeaders.TryAddWithoutValidation(
            "X-Compatibility-Date",
            CompatibilityDate);

        http.DefaultRequestHeaders.UserAgent.ParseAdd("EveOnTrader.Worker/1.0");

        return http;
    }

    private static IEnumerable<List<long>> Batch(List<long> source, int batchSize)
    {
        for (var i = 0; i < source.Count; i += batchSize)
        {
            yield return source.Skip(i).Take(batchSize).ToList();
        }
    }
}
