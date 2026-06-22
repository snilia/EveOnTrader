using EveOnTrader.Core.DealFinding.Models;
using EveOnTrader.Core.ReadModels;
using EveOnTrader.Core.RouteFinding;

namespace EveOnTrader.Core.DealFinding.Services;

// StationToStationDealFinder finds deals for all item types between one source station and one destination station.
public class StationToStationDealFinder
{
    private readonly ItemRouteDealFinder _itemRouteDealFinder;
    private readonly IRouteDistanceService _routeDistanceService;

    // Creates station-to-station deal finder with item deal finder and route distance service.
    public StationToStationDealFinder(
        ItemRouteDealFinder itemRouteDealFinder,
        IRouteDistanceService routeDistanceService)
    {
        _itemRouteDealFinder = itemRouteDealFinder;
        _routeDistanceService = routeDistanceService;
    }

    // FindDealsAsync gets jump count once for the station pair, then runs item deal finder for every item.
    public async Task<RouteDealResult> FindDealsAsync(
        AllItemTypesOrderRoute route,
        RouteSecurityPreference routeSecurityPreference,
        DealFinderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(route);

        options ??= new DealFinderOptions();

        if (!route.SourceLocationId.HasValue)
        {
            return BuildEmptyRouteDealResult(route);
        }

        var jumpCount = await _routeDistanceService.GetJumpCountAsync(
            route.SourceLocationId.Value,
            route.DestinationLocationId,
            routeSecurityPreference);

        if (!jumpCount.HasValue)
        {
            return BuildEmptyRouteDealResult(route);
        }

        var safeJumpCount = jumpCount.Value > 0 ? jumpCount.Value : 1;
        var items = new List<ItemDealResult>();

        foreach (var itemRoute in route.Items)
        {
            var itemResult = _itemRouteDealFinder.FindItemDeals(itemRoute, safeJumpCount, options);

            if (itemResult != null && PassesStationToStationFilters(itemResult, options))
            {
                items.Add(itemResult);
            }
        }

        items = items
            .OrderByDescending(x => x.Steps.Max(step => step.TotalProfit))
            .ThenBy(x => x.TypeId)
            .ToList();

        var result = BuildEmptyRouteDealResult(route);
        result.JumpCount = safeJumpCount;
        result.Items = items;

        return result;
    }

    // PassesStationToStationFilters checks filters that need final route-level profit values.
    private static bool PassesStationToStationFilters(ItemDealResult itemResult, DealFinderOptions options)
    {
        var bestStep = itemResult.Steps.Last();

        if (options.MinTotalProfit.HasValue && bestStep.TotalProfit < options.MinTotalProfit.Value)
        {
            return false;
        }

        if (options.MinProfitPerJump.HasValue && bestStep.TotalProfitPerJump < options.MinProfitPerJump.Value)
        {
            return false;
        }

        return true;
    }

    // BuildEmptyRouteDealResult creates route-level result metadata even when no deals are found.
    private static RouteDealResult BuildEmptyRouteDealResult(AllItemTypesOrderRoute route)
    {
        var firstSourceOrder = route.Items
            .SelectMany(x => x.SourceSellOrders)
            .FirstOrDefault();

        var firstDestinationOrder = route.Items
            .SelectMany(x => x.DestinationBuyOrders)
            .FirstOrDefault();

        return new RouteDealResult
        {
            SourceRegionId = firstSourceOrder?.RegionId ?? route.SourceRegionId,
            SourceRegionName = firstSourceOrder?.RegionName ?? "",
            SourceLocationId = route.SourceLocationId,
            SourceLocationName = firstSourceOrder?.LocationName ?? "",

            DestinationRegionId = firstDestinationOrder?.RegionId,
            DestinationRegionName = firstDestinationOrder?.RegionName ?? "",
            DestinationLocationId = route.DestinationLocationId,
            DestinationLocationName = firstDestinationOrder?.LocationName ?? "",

            ImportedAfterUtc = route.ImportedAfterUtc,
            JumpCount = 1,
            Items = []
        };
    }
}