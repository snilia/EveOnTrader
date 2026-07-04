using EveOnTrader.Core.Models;
using EveOnTrader.Infra.Data;
using EveOnTrader.Infra.Esi;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EveOnTrader.Infra.MarketImport;

// UniverseReferenceSyncService syncs universe reference rows needed by imports and deal queries.
public class UniverseReferenceSyncService
{
    private const int BatchSize = 500;

    private readonly AppDbContext _db;
    private readonly EsiUniverseClient _esiUniverseClient;
    private readonly ILogger<UniverseReferenceSyncService> _logger;

    // Creates universe sync service with DB access, ESI universe client, and logger.
    public UniverseReferenceSyncService(
        AppDbContext db,
        EsiUniverseClient esiUniverseClient,
        ILogger<UniverseReferenceSyncService> logger)
    {
        _db = db;
        _esiUniverseClient = esiUniverseClient;
        _logger = logger;
    }

    // Syncs regions and solar systems needed for core universe reference data.
    public async Task<UniverseReferenceSyncResult> SyncAsync(CancellationToken cancellationToken = default)
    {
        var regionsInserted = await SyncRegionsAsync(cancellationToken);
        var solarSystemsInserted = await SyncSolarSystemsAsync(cancellationToken);

        return new UniverseReferenceSyncResult
        {
            RegionsInserted = regionsInserted,
            SolarSystemsInserted = solarSystemsInserted
        };
    }

    // Downloads missing regions from ESI and saves IDs/names into Regions.
    public async Task<int> SyncRegionsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Checking regions...");

        var regionIds = await _esiUniverseClient.GetRegionIdsAsync(cancellationToken);

        var existingRegionIds = await _db.Regions
            .Select(x => x.RegionId)
            .ToHashSetAsync(cancellationToken);

        var missingRegionIds = regionIds
            .Where(id => !existingRegionIds.Contains(id))
            .OrderBy(id => id)
            .ToList();

        if (missingRegionIds.Count == 0)
        {
            _logger.LogInformation("No missing regions.");
            return 0;
        }

        _logger.LogInformation(
            "Found {MissingCount:n0} missing region IDs. Resolving names from ESI...",
            missingRegionIds.Count);

        var regionsToInsert = new List<Region>();

        foreach (var batch in Batch(missingRegionIds, BatchSize))
        {
            var results = await _esiUniverseClient.GetNamesAsync(batch, cancellationToken);

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
            _logger.LogInformation("ESI returned no new regions.");
            return 0;
        }

        _db.Regions.AddRange(regionsToInsert);
        await _db.SaveChangesAsync(cancellationToken);
        _db.ChangeTracker.Clear();

        _logger.LogInformation("Inserted {InsertedCount:n0} new regions.", regionsToInsert.Count);
        return regionsToInsert.Count;
    }

    // Downloads missing solar systems from ESI, resolves their region IDs, and saves them into SolarSystems.
    public async Task<int> SyncSolarSystemsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Checking solar systems...");

        var solarSystemIds = await _esiUniverseClient.GetSolarSystemIdsAsync(cancellationToken);

        var existingSolarSystemIds = await _db.SolarSystems
            .Select(x => x.SolarSystemId)
            .ToHashSetAsync(cancellationToken);

        var missingSolarSystemIds = solarSystemIds
            .Where(id => !existingSolarSystemIds.Contains(id))
            .OrderBy(id => id)
            .ToList();

        if (missingSolarSystemIds.Count == 0)
        {
            _logger.LogInformation("No missing solar systems.");
            return 0;
        }

        _logger.LogInformation(
            "Found {MissingCount:n0} missing solar system IDs. Resolving details from ESI...",
            missingSolarSystemIds.Count);

        var constellationRegionMap = new Dictionary<long, long>();
        var systemsToInsert = new List<SolarSystem>();
        var processed = 0;

        foreach (var solarSystemId in missingSolarSystemIds)
        {
            var systemResult = await _esiUniverseClient.GetSolarSystemAsync(
                solarSystemId,
                cancellationToken);

            if (systemResult is null || string.IsNullOrWhiteSpace(systemResult.Name))
            {
                continue;
            }

            if (!constellationRegionMap.TryGetValue(systemResult.ConstellationId, out var regionId))
            {
                var constellationResult = await _esiUniverseClient.GetConstellationAsync(
                    systemResult.ConstellationId,
                    cancellationToken);

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
                _logger.LogInformation(
                    "Resolved {Processed:n0}/{Total:n0} solar systems...",
                    processed,
                    missingSolarSystemIds.Count);
            }
        }

        if (systemsToInsert.Count == 0)
        {
            _logger.LogInformation("ESI returned no new solar systems.");
            return 0;
        }

        _db.SolarSystems.AddRange(systemsToInsert);
        await _db.SaveChangesAsync(cancellationToken);
        _db.ChangeTracker.Clear();

        _logger.LogInformation("Inserted {InsertedCount:n0} new solar systems.", systemsToInsert.Count);
        return systemsToInsert.Count;
    }

    // Resolves missing market locations from imported orders and saves station/structure rows.
    public async Task<int> SyncMarketLocationsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Checking market locations...");

        var orderLocations = await _db.MarketOrders
            .Select(x => new { x.LocationId, x.SystemId })
            .Distinct()
            .ToListAsync(cancellationToken);

        var existingLocationIds = await _db.MarketLocations
            .Select(x => x.LocationId)
            .ToHashSetAsync(cancellationToken);

        var missingLocations = orderLocations
            .Where(x => x.LocationId > 0 && !existingLocationIds.Contains(x.LocationId))
            .OrderBy(x => x.LocationId)
            .ToList();

        if (missingLocations.Count == 0)
        {
            _logger.LogInformation("No missing market locations.");
            return 0;
        }

        _logger.LogInformation(
            "Found {MissingCount:n0} missing market locations. Resolving details from ESI...",
            missingLocations.Count);

        var publicStructureIdSet = new HashSet<long>();

        if (missingLocations.Any(x => !IsStationId(x.LocationId)))
        {
            var publicStructureIds = await _esiUniverseClient.GetPublicStructureIdsAsync(cancellationToken);
            publicStructureIdSet = publicStructureIds.ToHashSet();
        }

        var locationsToInsert = new List<MarketLocation>();

        foreach (var entry in missingLocations)
        {
            if (IsStationId(entry.LocationId))
            {
                var stationResult = await _esiUniverseClient.TryGetStationAsync(
                    entry.LocationId,
                    cancellationToken);

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
            //if not a station, or station details not found, add unknown structure
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
                //public docking if station or public structure
                HasPublicDocking = IsStationId(entry.LocationId)
                    ? true
                    : publicStructureIdSet.Contains(entry.LocationId)
            });
        }

        if (locationsToInsert.Count == 0)
        {
            _logger.LogInformation("ESI returned no new market locations.");
            return 0;
        }

        _db.MarketLocations.AddRange(locationsToInsert);
        await _db.SaveChangesAsync(cancellationToken);
        _db.ChangeTracker.Clear();

        _logger.LogInformation("Inserted {InsertedCount:n0} new market locations.", locationsToInsert.Count);
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

// UniverseReferenceSyncResult summarizes region/system sync.
public class UniverseReferenceSyncResult
{
    public int RegionsInserted { get; set; }
    public int SolarSystemsInserted { get; set; }
}