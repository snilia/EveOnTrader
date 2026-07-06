using EveOnTrader.Core.Models;
using EveOnTrader.Core.RouteFinding;
using EveOnTrader.Infra.Data;
using EveOnTrader.Infra.Esi;
using Microsoft.EntityFrameworkCore;

namespace EveOnTrader.Infra.RouteFinding;

// StationDistanceFinder returns route jump counts between market locations using DB cache and ESI.
public class StationDistanceFinder : IStationDistanceFinder
{
    private readonly AppDbContext _db;
    private readonly EsiDistanceClient _esiDistanceClient;

    // Creates distance finder with DB cache and ESI distance client.
    public StationDistanceFinder(
        AppDbContext db,
        EsiDistanceClient esiDistanceClient)
    {
        _db = db;
        _esiDistanceClient = esiDistanceClient;
    }

    // GetJumpCountAsync returns jump count for one source/destination location pair.
    public async Task<int?> GetJumpCountAsync(
        long sourceLocationId,
        long destinationLocationId,
        RouteSecurityPreference securityPreference)
    {
        var results = await GetJumpCountsAsync(
            [sourceLocationId],
            [destinationLocationId],
            securityPreference);

        return results.TryGetValue((sourceLocationId, destinationLocationId), out var jumpCount)
            ? jumpCount
            : null;
    }

    // GetJumpCountsAsync returns jump counts for every source x destination location pair.
    public async Task<Dictionary<(long SourceLocationId, long DestinationLocationId), int?>> GetJumpCountsAsync(
        List<long> sourceLocationIds,
        List<long> destinationLocationIds,
        RouteSecurityPreference securityPreference)
    {
        ArgumentNullException.ThrowIfNull(sourceLocationIds);
        ArgumentNullException.ThrowIfNull(destinationLocationIds);

        var distinctSourceLocationIds = NormalizeIds(sourceLocationIds);
        var distinctDestinationLocationIds = NormalizeIds(destinationLocationIds);

        var result = new Dictionary<(long SourceLocationId, long DestinationLocationId), int?>();

        if (distinctSourceLocationIds.Count == 0 || distinctDestinationLocationIds.Count == 0)
        {
            return result;
        }

        var allLocationIds = distinctSourceLocationIds
            .Concat(distinctDestinationLocationIds)
            .Distinct()
            .ToList();

        var locationSystemMap = await _db.MarketLocations
            .AsNoTracking()
            .Where(x => allLocationIds.Contains(x.LocationId))
            .Select(x => new
            {
                x.LocationId,
                x.SolarSystemId
            })
            .ToDictionaryAsync(x => x.LocationId, x => x.SolarSystemId);

        var systemPairs = BuildSystemPairs(
            distinctSourceLocationIds,
            distinctDestinationLocationIds,
            locationSystemMap);

        if (systemPairs.Count == 0)
        {
            return result;
        }

        var cachedDistances = await LoadCachedDistancesAsync(systemPairs, securityPreference);
        var missingSystemPairs = systemPairs
            .Where(pair => !cachedDistances.ContainsKey(pair))
            .ToList();

        if (missingSystemPairs.Count > 0)
        {
            var newDistances = await FetchAndSaveMissingDistancesAsync(
                missingSystemPairs,
                securityPreference);

            foreach (var entry in newDistances)
            {
                cachedDistances[entry.Key] = entry.Value;
            }
        }

        foreach (var sourceLocationId in distinctSourceLocationIds)
        {
            if (!locationSystemMap.TryGetValue(sourceLocationId, out var sourceSystemId))
            {
                continue;
            }

            foreach (var destinationLocationId in distinctDestinationLocationIds)
            {
                if (!locationSystemMap.TryGetValue(destinationLocationId, out var destinationSystemId))
                {
                    continue;
                }

                result[(sourceLocationId, destinationLocationId)] =
                    cachedDistances.TryGetValue((sourceSystemId, destinationSystemId), out var jumpCount)
                        ? jumpCount
                        : null;
            }
        }

        return result;
    }

    // NormalizeIds removes invalid and duplicate IDs.
    private static List<long> NormalizeIds(IEnumerable<long> ids)
    {
        return ids
            .Where(x => x > 0)
            .Distinct()
            .ToList();
    }

    // BuildSystemPairs builds distinct system pairs needed for all requested location pairs.
    private static List<(long SourceSolarSystemId, long DestinationSolarSystemId)> BuildSystemPairs(
        List<long> sourceLocationIds,
        List<long> destinationLocationIds,
        Dictionary<long, long> locationSystemMap)
    {
        var pairs = new HashSet<(long SourceSolarSystemId, long DestinationSolarSystemId)>();

        foreach (var sourceLocationId in sourceLocationIds)
        {
            if (!locationSystemMap.TryGetValue(sourceLocationId, out var sourceSystemId))
            {
                continue;
            }

            foreach (var destinationLocationId in destinationLocationIds)
            {
                if (!locationSystemMap.TryGetValue(destinationLocationId, out var destinationSystemId))
                {
                    continue;
                }

                pairs.Add((sourceSystemId, destinationSystemId));
            }
        }

        return pairs.ToList();
    }

    // LoadCachedDistancesAsync loads already-resolved distances from DB cache.
    private async Task<Dictionary<(long SourceSolarSystemId, long DestinationSolarSystemId), int?>> LoadCachedDistancesAsync(
        List<(long SourceSolarSystemId, long DestinationSolarSystemId)> systemPairs,
        RouteSecurityPreference securityPreference)
    {
        var sourceSystemIds = systemPairs
            .Select(x => x.SourceSolarSystemId)
            .Distinct()
            .ToList();

        var destinationSystemIds = systemPairs
            .Select(x => x.DestinationSolarSystemId)
            .Distinct()
            .ToList();

        var cacheRows = await _db.SystemDistanceCaches
            .AsNoTracking()
            .Where(x =>
                x.SecurityPreference == securityPreference &&
                sourceSystemIds.Contains(x.SourceSolarSystemId) &&
                destinationSystemIds.Contains(x.DestinationSolarSystemId))
            .ToListAsync();

        var requestedPairs = systemPairs.ToHashSet();

        return cacheRows
            .Where(x => requestedPairs.Contains((x.SourceSolarSystemId, x.DestinationSolarSystemId)))
            .ToDictionary(
                x => (x.SourceSolarSystemId, x.DestinationSolarSystemId),
                x => x.JumpCount);
    }

    // FetchAndSaveMissingDistancesAsync gets missing distances from ESI, validates them, and saves them to cache.
    private async Task<Dictionary<(long SourceSolarSystemId, long DestinationSolarSystemId), int?>> FetchAndSaveMissingDistancesAsync(
        List<(long SourceSolarSystemId, long DestinationSolarSystemId)> missingSystemPairs,
        RouteSecurityPreference securityPreference)
    {
        var resolvedAtUtc = DateTime.UtcNow;
        var results = new Dictionary<(long SourceSolarSystemId, long DestinationSolarSystemId), int?>();
        var cacheRows = new List<SystemDistanceCache>();

        foreach (var pair in missingSystemPairs)
        {
            var routeSolarSystemIds = await _esiDistanceClient.GetRouteSolarSystemIdsAsync(
                pair.SourceSolarSystemId,
                pair.DestinationSolarSystemId,
                securityPreference);

            var jumpCount = await GetValidatedJumpCountAsync(
                routeSolarSystemIds,
                securityPreference);

            results[pair] = jumpCount;

            cacheRows.Add(new SystemDistanceCache
            {
                SourceSolarSystemId = pair.SourceSolarSystemId,
                DestinationSolarSystemId = pair.DestinationSolarSystemId,
                SecurityPreference = securityPreference,
                JumpCount = jumpCount,
                ResolvedAtUtc = resolvedAtUtc
            });
        }

        if (cacheRows.Count > 0)
        {
            try
            {
                _db.SystemDistanceCaches.AddRange(cacheRows);
                await _db.SaveChangesAsync();
                _db.ChangeTracker.Clear();
            }
            catch
            {
                _db.ChangeTracker.Clear();
                throw;
            }
        }

        return results;
    }

    // GetValidatedJumpCountAsync returns jump count, or null if route violates selected security rule.
    private async Task<int?> GetValidatedJumpCountAsync(
        List<long>? routeSolarSystemIds,
        RouteSecurityPreference securityPreference)
    {
        if (routeSolarSystemIds is null || routeSolarSystemIds.Count == 0)
        {
            return null;
        }

        if (securityPreference == RouteSecurityPreference.Secure)
        {
            var routeSystemIdSet = routeSolarSystemIds
                .Distinct()
                .ToList();

            var highSecSystemCount = await _db.SolarSystems
                .AsNoTracking()
                .Where(x =>
                    routeSystemIdSet.Contains(x.SolarSystemId) &&
                    x.SecurityStatus >= 0.45)
                .CountAsync();

            if (highSecSystemCount != routeSystemIdSet.Count)
            {
                return null;
            }
        }

        return Math.Max(0, routeSolarSystemIds.Count - 1);
    }
}