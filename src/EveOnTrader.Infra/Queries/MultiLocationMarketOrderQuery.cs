using EveOnTrader.Core.DealFinding.Services;
using EveOnTrader.Core.ReadModels;
using EveOnTrader.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace EveOnTrader.Infra.Queries;

// MultiLocationMarketOrderQuery loads market order rows for multi-location deal searches.
public class MultiLocationMarketOrderQuery : IMultiLocationMarketOrderQuery
{
    private readonly AppDbContext _db;

    // Creates multi-location market order query with database access.
    public MultiLocationMarketOrderQuery(AppDbContext db)
    {
        _db = db;
    }

    // GetMarketOrderRowsAsync returns source sell rows and destination buy rows for all requested locations.
    public async Task<MultiLocationMarketOrderRows> GetMarketOrderRowsAsync(
        List<long> sourceLocationIds,
        List<long> destinationLocationIds,
        DateTime? importedAfterUtc = null)
    {
        ArgumentNullException.ThrowIfNull(sourceLocationIds);
        ArgumentNullException.ThrowIfNull(destinationLocationIds);

        var distinctSourceLocationIds = sourceLocationIds
            .Where(x => x > 0)
            .Distinct()
            .ToList();

        var distinctDestinationLocationIds = destinationLocationIds
            .Where(x => x > 0)
            .Distinct()
            .ToList();

        var sourceSellOrders = await GetOrderRowsAsync(
            distinctSourceLocationIds,
            false,
            importedAfterUtc);

        var destinationBuyOrders = await GetOrderRowsAsync(
            distinctDestinationLocationIds,
            true,
            importedAfterUtc);

        return new MultiLocationMarketOrderRows
        {
            SourceSellOrders = sourceSellOrders,
            DestinationBuyOrders = destinationBuyOrders
        };
    }

    // GetOrderRowsAsync loads enriched order rows for one side of a market route search.
    private async Task<List<ItemOrderRow>> GetOrderRowsAsync(
        List<long> locationIds,
        bool isBuyOrder,
        DateTime? importedAfterUtc)
    {
        if (locationIds.Count == 0)
        {
            return [];
        }

        var candidateOrders = _db.MarketOrders
            .AsNoTracking()
            .Where(o =>
                locationIds.Contains(o.LocationId) &&
                o.IsBuyOrder == isBuyOrder);

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

        var rows = await query.ToListAsync();

        return ApplyOrder(rows, isBuyOrder);
    }

    // ApplyOrder keeps source sells cheapest-first and destination buys highest-first inside each location/type.
    private static List<ItemOrderRow> ApplyOrder(
        List<ItemOrderRow> rows,
        bool isBuyOrder)
    {
        if (isBuyOrder)
        {
            return rows
                .OrderBy(x => x.LocationId)
                .ThenBy(x => x.TypeId)
                .ThenByDescending(x => x.Price)
                .ToList();
        }

        return rows
            .OrderBy(x => x.LocationId)
            .ThenBy(x => x.TypeId)
            .ThenBy(x => x.Price)
            .ToList();
    }
}