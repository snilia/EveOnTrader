namespace EveOnTrader.Core.DealFinding.Services;

// IRegionToLocationsQuery returns market location IDs that belong to regions.
public interface IRegionToLocationsQuery
{
    // GetLocationIdsInRegionsAsync returns distinct market location IDs for the given region IDs.
    Task<List<long>> GetLocationIdsInRegionsAsync(List<long> regionIds);
}