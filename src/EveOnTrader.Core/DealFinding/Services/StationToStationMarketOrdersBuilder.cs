using EveOnTrader.Core.ReadModels;

namespace EveOnTrader.Core.DealFinding.Services;

// StationToStationMarketOrdersBuilder builds station-to-station order routes from preloaded market order rows.
public class StationToStationMarketOrdersBuilder
{
    // BuildRoutes lazily creates grouped item routes for every source/destination location pair.
    // Enumerate returned sequence once; each enumeration rebuilds route objects.
    public IEnumerable<AllItemTypesOrderRoute> BuildRoutes(
        MultiLocationMarketOrderRows marketOrderRows,
        List<long> sourceLocationIds,
        List<long> destinationLocationIds,
        DateTime? importedAfterUtc = null)
    {
        ArgumentNullException.ThrowIfNull(marketOrderRows);
        ArgumentNullException.ThrowIfNull(sourceLocationIds);
        ArgumentNullException.ThrowIfNull(destinationLocationIds);

        var distinctSourceLocationIds = NormalizeLocationIds(sourceLocationIds);
        var distinctDestinationLocationIds = NormalizeLocationIds(destinationLocationIds);

        var sourceOrdersByLocation = BuildOrdersByLocationAndType(marketOrderRows.SourceSellOrders);
        var destinationOrdersByLocation = BuildOrdersByLocationAndType(marketOrderRows.DestinationBuyOrders);

        foreach (var sourceLocationId in distinctSourceLocationIds)
        {
            if (!sourceOrdersByLocation.TryGetValue(sourceLocationId, out var sourceOrdersByType))
            {
                continue;
            }

            var firstSourceOrder = GetFirstOrder(sourceOrdersByType);

            if (firstSourceOrder == null)
            {
                continue;
            }

            foreach (var destinationLocationId in distinctDestinationLocationIds)
            {
                if (!destinationOrdersByLocation.TryGetValue(destinationLocationId, out var destinationOrdersByType))
                {
                    continue;
                }

                var itemRoutes = BuildItemRoutes(sourceOrdersByType, destinationOrdersByType);

                if (itemRoutes.Count == 0)
                {
                    continue;
                }
                //yield return lets the caller use foreach for each result, without building a full list in memory. 
                //Each yield return creates a new AllItemTypesOrderRoute object for the current source/destination pair.  
                //like a conveyor belt instead of a box full of items. 
                yield return new AllItemTypesOrderRoute
                {
                    SourceRegionId = firstSourceOrder.RegionId,
                    SourceLocationId = sourceLocationId,
                    DestinationLocationId = destinationLocationId,
                    ImportedAfterUtc = importedAfterUtc,
                    Items = itemRoutes
                };
            }
        }
    }

    // NormalizeLocationIds removes invalid and duplicate location IDs once.
    private static List<long> NormalizeLocationIds(List<long> locationIds)
    {
        return locationIds
            .Where(x => x > 0)
            .Distinct()
            .ToList();
    }

    // BuildOrdersByLocationAndType indexes already-sorted order rows by location and item type.
    private static Dictionary<long, Dictionary<long, List<ItemOrderRow>>> BuildOrdersByLocationAndType(
        List<ItemOrderRow> orderRows)
    {
        return orderRows
            .GroupBy(x => x.LocationId)
            .ToDictionary(
                locationGroup => locationGroup.Key,
                locationGroup => locationGroup
                    .GroupBy(x => x.TypeId)
                    .ToDictionary(
                        typeGroup => typeGroup.Key,
                        typeGroup => typeGroup.ToList()));
    }

    // BuildItemRoutes creates item routes only for item types that exist on both sides.
    private static List<SingleItemTypeOrderRoute> BuildItemRoutes(
        Dictionary<long, List<ItemOrderRow>> sourceOrdersByType,
        Dictionary<long, List<ItemOrderRow>> destinationOrdersByType)
    {
        var smallerOrderBook = sourceOrdersByType.Count <= destinationOrdersByType.Count
            ? sourceOrdersByType
            : destinationOrdersByType;

        var itemRoutes = new List<SingleItemTypeOrderRoute>();

        foreach (var typeId in smallerOrderBook.Keys)
        {
            if (!sourceOrdersByType.TryGetValue(typeId, out var sourceOrders) ||
                !destinationOrdersByType.TryGetValue(typeId, out var destinationOrders))
            {
                continue;
            }

            var sourceOrder = sourceOrders[0];
            var destinationOrder = destinationOrders[0];

            itemRoutes.Add(new SingleItemTypeOrderRoute
            {
                TypeId = typeId,
                TypeName = GetTypeName(sourceOrder, destinationOrder),
                UnitVolumeM3 = GetUnitVolumeM3(sourceOrder, destinationOrder),
                SourceSellOrders = sourceOrders,
                DestinationBuyOrders = destinationOrders
            });
        }

        return itemRoutes;
    }

    // GetFirstOrder returns first order from indexed order rows.
    private static ItemOrderRow? GetFirstOrder(Dictionary<long, List<ItemOrderRow>> ordersByType)
    {
        foreach (var orders in ordersByType.Values)
        {
            if (orders.Count > 0)
            {
                return orders[0];
            }
        }

        return null;
    }

    // GetTypeName returns useful item name from source or destination row.
    private static string GetTypeName(ItemOrderRow sourceOrder, ItemOrderRow destinationOrder)
    {
        if (!string.IsNullOrWhiteSpace(sourceOrder.TypeName) && sourceOrder.TypeName != "(unknown item)")
        {
            return sourceOrder.TypeName;
        }

        if (!string.IsNullOrWhiteSpace(destinationOrder.TypeName) && destinationOrder.TypeName != "(unknown item)")
        {
            return destinationOrder.TypeName;
        }

        return "(unknown item)";
    }

    // GetUnitVolumeM3 returns useful item volume from source or destination row.
    private static decimal GetUnitVolumeM3(ItemOrderRow sourceOrder, ItemOrderRow destinationOrder)
    {
        if (sourceOrder.UnitVolumeM3 > 0m)
        {
            return sourceOrder.UnitVolumeM3;
        }

        if (destinationOrder.UnitVolumeM3 > 0m)
        {
            return destinationOrder.UnitVolumeM3;
        }

        return 0m;
    }
}