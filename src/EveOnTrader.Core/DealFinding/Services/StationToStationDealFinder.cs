using EveOnTrader.Core.DealFinding.Models;
using EveOnTrader.Core.ReadModels;

namespace EveOnTrader.Core.DealFinding.Services;

// StationToStationDealFinder finds deals for all item types between one source station and one destination station.
public class StationToStationDealFinder
{
    private readonly ItemRouteDealFinder _itemRouteDealFinder;

    // Creates station-to-station deal finder with item deal finder.
    public StationToStationDealFinder(ItemRouteDealFinder itemRouteDealFinder)
    {
        _itemRouteDealFinder = itemRouteDealFinder;
    }

    // FindDeals finds item deals for one station pair using already-known jump count.
    public RouteDealResult FindDeals(
        AllItemTypesOrderRoute route,
        int jumpCount,
        DealFinderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(route);

        options ??= new DealFinderOptions();

        if (!route.SourceLocationId.HasValue)
        {
            return BuildEmptyRouteDealResult(route);
        }

        var safeJumpCount = jumpCount > 0 ? jumpCount : 1;
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