using EveOnTrader.Core.DealFinding.Services;
using EveOnTrader.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace EveOnTrader.Infra.Queries;

// RegionToLocationsQuery gets market location IDs inside regions.
public class RegionToLocationsQuery : IRegionToLocationsQuery
{
    private readonly AppDbContext _db;

    // Creates region-to-locations query with database access.
    public RegionToLocationsQuery(AppDbContext db)
    {
        _db = db;
    }

    // GetLocationIdsInRegionsAsync returns distinct market location IDs for the given region IDs.
    public async Task<List<long>> GetLocationIdsInRegionsAsync(List<long> regionIds)
    {
        if (regionIds.Count == 0)
        {
            return [];
        }

        var distinctRegionIds = regionIds
            .Distinct()
            .ToList();

        return await (
            from location in _db.MarketLocations.AsNoTracking()
            join system in _db.SolarSystems.AsNoTracking()
                on location.SolarSystemId equals system.SolarSystemId
            where distinctRegionIds.Contains(system.RegionId)
            select location.LocationId
        )
        .Distinct()
        .OrderBy(x => x)
        .ToListAsync();
    }
}