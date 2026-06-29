using EveOnTrader.Core.DealFinding.Services;
using EveOnTrader.Core.ReadModels;
using EveOnTrader.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace EveOnTrader.Infra.Queries;

// SingleItemStationToStationQuery loads all order books for one item between two stations.
public class SingleItemStationToStationQuery : ISingleItemStationToStationQuery
{
    private readonly AppDbContext _db;

    // Creates single-item station-to-station query with database access.
    public SingleItemStationToStationQuery(AppDbContext db)
    {
        _db = db;
    }

    // GetOrdersAsync returns source/destination buy and sell orders for one item.
    public async Task<SingleItemStationToStationOrders> GetOrdersAsync(
        long typeId,
        long sourceLocationId,
        long destinationLocationId,
        DateTime? importedAfterUtc = null)
    {
        var rows = await GetOrderRowsAsync(
            typeId,
            sourceLocationId,
            destinationLocationId,
            importedAfterUtc);

        var sourceRows = rows
            .Where(x => x.LocationId == sourceLocationId)
            .ToList();

        var destinationRows = rows
            .Where(x => x.LocationId == destinationLocationId)
            .ToList();

        var firstSourceOrder = sourceRows.FirstOrDefault();
        var firstDestinationOrder = destinationRows.FirstOrDefault();

        return new SingleItemStationToStationOrders
        {
            TypeId = typeId,
            TypeName = GetTypeName(rows),
            UnitVolumeM3 = GetUnitVolumeM3(rows),

            SourceLocationId = sourceLocationId,
            SourceLocationName = firstSourceOrder?.LocationName ?? "",
            SourceRegionId = firstSourceOrder?.RegionId ?? 0,
            SourceRegionName = firstSourceOrder?.RegionName ?? "",

            DestinationLocationId = destinationLocationId,
            DestinationLocationName = firstDestinationOrder?.LocationName ?? "",
            DestinationRegionId = firstDestinationOrder?.RegionId ?? 0,
            DestinationRegionName = firstDestinationOrder?.RegionName ?? "",

            SourceSellOrders = SortSellOrders(sourceRows.Where(x => !x.IsBuyOrder)),
            SourceBuyOrders = SortBuyOrders(sourceRows.Where(x => x.IsBuyOrder)),
            DestinationSellOrders = SortSellOrders(destinationRows.Where(x => !x.IsBuyOrder)),
            DestinationBuyOrders = SortBuyOrders(destinationRows.Where(x => x.IsBuyOrder))
        };
    }

    // GetOrderRowsAsync loads enriched order rows for both stations and one item type.
    private async Task<List<ItemOrderRow>> GetOrderRowsAsync(
        long typeId,
        long sourceLocationId,
        long destinationLocationId,
        DateTime? importedAfterUtc)
    {
        var candidateOrders = _db.MarketOrders
            .AsNoTracking()
            .Where(o =>
                o.TypeId == typeId &&
                (o.LocationId == sourceLocationId || o.LocationId == destinationLocationId));

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

    // SortSellOrders sorts sell orders cheapest first.
    private static List<ItemOrderRow> SortSellOrders(IEnumerable<ItemOrderRow> orders)
    {
        return orders
            .OrderBy(x => x.Price)
            .ToList();
    }

    // SortBuyOrders sorts buy orders highest first.
    private static List<ItemOrderRow> SortBuyOrders(IEnumerable<ItemOrderRow> orders)
    {
        return orders
            .OrderByDescending(x => x.Price)
            .ToList();
    }

    // GetTypeName returns first useful item name.
    private static string GetTypeName(List<ItemOrderRow> rows)
    {
        return rows
            .Select(x => x.TypeName)
            .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x) && x != "(unknown item)")
            ?? "(unknown item)";
    }

    // GetUnitVolumeM3 returns first useful item volume.
    private static decimal GetUnitVolumeM3(List<ItemOrderRow> rows)
    {
        return rows
            .Select(x => x.UnitVolumeM3)
            .FirstOrDefault(x => x > 0m);
    }
}