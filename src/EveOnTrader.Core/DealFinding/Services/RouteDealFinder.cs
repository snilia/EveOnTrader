using EveOnTrader.Core.DealFinding.Models;
using EveOnTrader.Core.ReadModels;

namespace EveOnTrader.Core.DealFinding.Services;

//only route-level orchestration, loop all items, call item finder, drop null results, sort final list, return RouteDealResult.cs
// RouteDealFinder builds route-wide deal results by running single-item finder for every item in route.
public class RouteDealFinder
{
    private readonly ItemRouteDealFinder _itemRouteDealFinder;

    // Creates route finder with single-item finder dependency.
    public RouteDealFinder(ItemRouteDealFinder itemRouteDealFinder)
    {
        _itemRouteDealFinder = itemRouteDealFinder;
    }

    // FindRouteDeals builds route result from all item routes that produce at least one valid deal result.
    public RouteDealResult FindRouteDeals(AllItemTypesOrderRoute route, DealFinderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(route);

        if (options == null)
        {
            options = new DealFinderOptions();
        }

        var jumpCount = options.JumpCount > 0 ? options.JumpCount : 1;
        var items = new List<ItemDealResult>();

        foreach (var itemRoute in route.Items)
        {
            var itemResult = _itemRouteDealFinder.FindItemDeals(itemRoute, options);

            if (itemResult != null)
            {
                items.Add(itemResult);
            }
        }

        items = items
            .OrderByDescending(x => x.Steps.Max(step => step.TotalProfit))
            .ThenBy(x => x.TypeId)
            .ToList();

        return new RouteDealResult
        {
            SourceRegionId = route.SourceRegionId,
            SourceLocationId = route.SourceLocationId,
            DestinationLocationId = route.DestinationLocationId,
            ImportedAfterUtc = route.ImportedAfterUtc,
            JumpCount = jumpCount,
            Items = items
        };
    }
}
