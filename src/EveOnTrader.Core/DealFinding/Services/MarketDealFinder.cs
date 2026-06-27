using EveOnTrader.Core.DealFinding.Models;
using EveOnTrader.Core.RouteFinding;

namespace EveOnTrader.Core.DealFinding.Services;

// MarketDealFinder finds deals for many source locations and many destination locations.
public class MarketDealFinder
{
    private readonly IRegionToLocationsQuery _regionToLocationsQuery;
    private readonly IMultiLocationMarketOrderQuery _multiLocationMarketOrderQuery;
    private readonly IBulkDistanceFinder _bulkDistanceFinder;
    private readonly StationToStationMarketOrdersBuilder _stationToStationMarketOrdersBuilder;
    private readonly StationToStationDealFinder _stationToStationDealFinder;

    // Creates market deal finder with queries, builders, and station-to-station deal finder.
    public MarketDealFinder(
        IRegionToLocationsQuery regionToLocationsQuery, //turns source region IDs into station/location IDs
        IMultiLocationMarketOrderQuery multiLocationMarketOrderQuery, //loads all source sell orders and destination buy orders in bulk
        IBulkDistanceFinder bulkDistanceFinder, //gets jump counts for all source/destination station pairs
        StationToStationMarketOrdersBuilder stationToStationMarketOrdersBuilder, //turns bulk orders into route objects
        StationToStationDealFinder stationToStationDealFinder) //finds deals for one source/destination station pair
    {
        _regionToLocationsQuery = regionToLocationsQuery;
        _multiLocationMarketOrderQuery = multiLocationMarketOrderQuery;
        _bulkDistanceFinder = bulkDistanceFinder;
        _stationToStationMarketOrdersBuilder = stationToStationMarketOrdersBuilder;
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
        ArgumentNullException.ThrowIfNull(sourceLocationIds);
        ArgumentNullException.ThrowIfNull(sourceRegionIds);
        ArgumentNullException.ThrowIfNull(destinationLocationIds);

        options ??= new DealFinderOptions();

        var sourceLocationIdsFromRegions = await _regionToLocationsQuery.GetLocationIdsInRegionsAsync(sourceRegionIds);

        var distinctSourceLocationIds = NormalizeLocationIds(
            sourceLocationIds.Concat(sourceLocationIdsFromRegions));

        var distinctDestinationLocationIds = NormalizeLocationIds(destinationLocationIds);

        if (distinctSourceLocationIds.Count == 0 || distinctDestinationLocationIds.Count == 0)
        {
            return [];
        }

        var marketOrderRows = await _multiLocationMarketOrderQuery.GetMarketOrderRowsAsync(
            distinctSourceLocationIds,
            distinctDestinationLocationIds,
            importedAfterUtc);

        var jumpCounts = await _bulkDistanceFinder.GetJumpCountsAsync(
            distinctSourceLocationIds,
            distinctDestinationLocationIds,
            routeSecurityPreference);

        var routeResults = new List<RouteDealResult>();

        var routes = _stationToStationMarketOrdersBuilder.BuildRoutes(
            marketOrderRows,
            distinctSourceLocationIds,
            distinctDestinationLocationIds,
            importedAfterUtc);

        foreach (var route in routes)
        {
            if (!route.SourceLocationId.HasValue)
            {
                continue;
            }

            if (!jumpCounts.TryGetValue(
                    (route.SourceLocationId.Value, route.DestinationLocationId),
                    out var jumpCount) ||
                !jumpCount.HasValue)
            {
                continue;
            }

            var routeResult = _stationToStationDealFinder.FindDeals(
                route,
                jumpCount.Value,
                options);

            if (routeResult.Items.Count > 0)
            {
                routeResults.Add(routeResult);
            }
        }

        return routeResults;
    }

    // NormalizeLocationIds removes invalid and duplicate location IDs.
    private static List<long> NormalizeLocationIds(IEnumerable<long> locationIds)
    {
        return locationIds
            .Where(x => x > 0)
            .Distinct()
            .ToList();
    }
}