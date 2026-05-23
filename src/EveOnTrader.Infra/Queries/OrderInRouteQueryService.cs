using EveOnTrader.Core.ReadModels;
using EveOnTrader.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace EveOnTrader.Infra.Queries;

// OrderInRouteQueryService loads and groups market orders for one source-to-destination trade route.
public class OrderInRouteQueryService
{
    private readonly AppDbContext _db;

    // Creates route-order query service with database access.
    public OrderInRouteQueryService(AppDbContext db)
    {
        _db = db;
    }

    // Loads and groups source-region sells with destination-location buys for one route, with optional freshness cutoff.
    public async Task<AllItemTypesOrderRoute> GetAllItemTypesOrderRouteAsync(
        long sourceRegionId,
        long destinationLocationId,
        DateTime? importedAfterUtc = null)
    {
        //query all orders for the route, all items, both sides
        var rows = await GetItemOrderRowsAsync(
            sourceRegionId,
            null,
            destinationLocationId,
            importedAfterUtc);

        return new AllItemTypesOrderRoute
        {
            SourceRegionId = sourceRegionId,
            SourceLocationId = null,
            DestinationLocationId = destinationLocationId,
            ImportedAfterUtc = importedAfterUtc,
            Items = BuildItemRoutes(rows)
        };
    }

    // Loads and groups source-location sells with destination-location buys for one route, with optional freshness cutoff.
    public async Task<AllItemTypesOrderRoute> GetAllItemTypesOrderRouteByLocationAsync(
        long sourceLocationId,
        long destinationLocationId,
        DateTime? importedAfterUtc = null)
    {
        //query all orders for the route, all items, both sides
        var rows = await GetItemOrderRowsAsync(
            null,
            sourceLocationId,
            destinationLocationId,
            importedAfterUtc);

        return new AllItemTypesOrderRoute
        {
            SourceRegionId = null,
            SourceLocationId = sourceLocationId,
            DestinationLocationId = destinationLocationId,
            ImportedAfterUtc = importedAfterUtc,
            Items = BuildItemRoutes(rows)
        };
    }

    // Loads raw source-side sells and destination-location buys for one route, with optional freshness cutoff.
    private async Task<List<ItemOrderRow>> GetItemOrderRowsAsync(
        long? sourceRegionId,
        long? sourceLocationId,
        long destinationLocationId,
        DateTime? importedAfterUtc)
    {
        var candidateOrders = _db.MarketOrders
            .AsNoTracking()
            .Where(o =>
                (
                    sourceRegionId.HasValue &&
                    o.RegionId == sourceRegionId.Value &&
                    !o.IsBuyOrder
                )
                ||
                (
                    sourceLocationId.HasValue &&
                    o.LocationId == sourceLocationId.Value &&
                    !o.IsBuyOrder
                )
                ||
                (
                    o.LocationId == destinationLocationId &&
                    o.IsBuyOrder
                ));

        if (importedAfterUtc.HasValue)
        {
            candidateOrders = candidateOrders
                .Where(o => o.ImportedAtUtc >= importedAfterUtc.Value);
        }

        var query =
            from o in candidateOrders

            join t in _db.ItemTypeRefs.AsNoTracking()
                on o.TypeId equals t.TypeId into typeRefs
            from t in typeRefs.DefaultIfEmpty()

            join region in _db.Regions.AsNoTracking()
                on o.RegionId equals region.RegionId into regionRefs
            from region in regionRefs.DefaultIfEmpty()

            join system in _db.SolarSystems.AsNoTracking()
                on o.SystemId equals system.SolarSystemId into systemRefs
            from system in systemRefs.DefaultIfEmpty()

            join location in _db.MarketLocations.AsNoTracking()
                on o.LocationId equals location.LocationId into locationRefs
            from location in locationRefs.DefaultIfEmpty()

            select new ItemOrderRow
            {
                OrderId = o.OrderId,
                IsBuyOrder = o.IsBuyOrder,
                Issued = o.Issued,
                RegionId = o.RegionId,
                SystemId = o.SystemId,
                LocationId = o.LocationId,
                TypeId = o.TypeId,
                Price = o.Price,
                VolumeRemain = o.VolumeRemain,
                VolumeTotal = o.VolumeTotal,
                MinVolume = o.MinVolume,
                Duration = o.Duration,
                Range = o.Range,
                ImportBatchId = o.ImportBatchId,
                ImportedAtUtc = o.ImportedAtUtc,
                UnitVolumeM3 = t != null ? t.VolumeM3 : 0m,
                TypeName = t != null ? t.Name : "(unknown item)",
                RegionName = region != null ? region.Name : "(unknown region)",
                SystemName = system != null ? system.Name : "(unknown system)",
                LocationName = location != null ? location.Name : "(unknown location)"
            };

        return await query.ToListAsync();
    }

    // Takes flat route rows and groups them into one sorted route object per item type.
    private static List<SingleItemTypeOrderRoute> BuildItemRoutes(List<ItemOrderRow> rows)
    {
        //take all raw orders from route, split them by item type, then group each item type's orders into source-side sells 
        //and destination-location buys, return all item groups as one list
        return rows
            .GroupBy(x => x.TypeId)
            .Select(g => new SingleItemTypeOrderRoute
            {
                TypeId = g.Key,
                TypeName = g
                    .Select(x => x.TypeName)
                    .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x) && x != "(unknown item)")
                    ?? "(unknown item)",
                UnitVolumeM3 = g
                    .Select(x => x.UnitVolumeM3)
                    .FirstOrDefault(x => x > 0m),
                SourceSellOrders = g
                    .Where(x => !x.IsBuyOrder)
                    .OrderBy(x => x.Price)
                    .ThenByDescending(x => x.VolumeRemain)
                    .ToList(),
                DestinationBuyOrders = g
                    .Where(x => x.IsBuyOrder)
                    .OrderByDescending(x => x.Price)
                    .ThenByDescending(x => x.VolumeRemain)
                    .ToList()
            })
            .OrderBy(x => x.TypeId)
            .ToList();
    }
}
