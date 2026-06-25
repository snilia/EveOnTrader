using EveOnTrader.Core.DealFinding.Models;
using EveOnTrader.Core.RouteFinding;

namespace EveOnTrader.Core.DealFinding.Services;

// MarketDealFinder finds deals for many source locations and many destination locations.
public class MarketDealFinder
{
    private readonly IStationToStationOrderRouteQuery _orderRouteQuery;
    private readonly IRegionToLocationsQuery _regionToLocationsQuery;
    private readonly StationToStationDealFinder _stationToStationDealFinder;

    // Creates market deal finder with order route query, region-to-locations query, and station-to-station deal finder.
    public MarketDealFinder(
        IStationToStationOrderRouteQuery orderRouteQuery,
        IRegionToLocationsQuery regionToLocationsQuery,
        StationToStationDealFinder stationToStationDealFinder)
    {
        _orderRouteQuery = orderRouteQuery;
        _regionToLocationsQuery = regionToLocationsQuery;
        _stationToStationDealFinder = stationToStationDealFinder;
    }

    // FindDealsAsync finds all station-to-station deal results for every source/destination location pair.
    public async Task<List<RouteDealResult>> FindDealsAsync(
        List<long> sourceLocationIds,
        List<long> sourceRegionIds,
        List<long> destinationLocationIds,
        RouteSecurityPreference routeSecurityPreference,
        DealFinderOptions? options = null,
        DateTime? importedAfterUtc = null)
    {
        options ??= new DealFinderOptions();

        var sourceLocationIdsFromRegions = await _regionToLocationsQuery.GetLocationIdsInRegionsAsync(sourceRegionIds);

        var distinctSourceLocationIds = sourceLocationIds
            .Concat(sourceLocationIdsFromRegions)
            .Where(x => x > 0)
            .Distinct()
            .ToList();

        var distinctDestinationLocationIds = destinationLocationIds
            .Where(x => x > 0)
            .Distinct()
            .ToList();

        var routeResults = new List<RouteDealResult>();

        foreach (var sourceLocationId in distinctSourceLocationIds)
        {
            foreach (var destinationLocationId in distinctDestinationLocationIds)
            {
                var route = await _orderRouteQuery.GetAllItemTypesOrderRouteAsync(
                    sourceLocationId,
                    destinationLocationId,
                    importedAfterUtc);

                var routeResult = await _stationToStationDealFinder.FindDealsAsync(
                    route,
                    routeSecurityPreference,
                    options);

                if (routeResult.Items.Count > 0)
                {
                    routeResults.Add(routeResult);
                }
            }
        }

        return routeResults;
    }
}