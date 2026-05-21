using EveOnTrader.Core.ReadModels;
using EveOnTrader.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace EveOnTrader.Infra.Queries;

// OrderQueryService loads order rows for web UI.
public class OrderQueryService
{
    private readonly AppDbContext _db;

    // Creates query service with database access.
    public OrderQueryService(AppDbContext db)
    {
        _db = db;
    }

    // Loads latest imported orders with names and import metadata for web display.
    public async Task<List<OrderListRow>> GetLatestOrdersAsync(int take = 200)
    {
        var query =
            from o in _db.MarketOrders.AsNoTracking()

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

            orderby o.ImportedAtUtc descending, o.Issued descending
            select new OrderListRow
            {
                OrderId = o.OrderId,
                IsBuyOrder = o.IsBuyOrder,
                Issued = o.Issued,
                ImportedAtUtc = o.ImportedAtUtc,
                ImportBatchId = o.ImportBatchId,
                TypeName = t != null ? t.Name : "(unknown item)",
                RegionName = region != null ? region.Name : "(unknown region)",
                SystemName = system != null ? system.Name : "(unknown system)",
                LocationName = location != null ? location.Name : "(unknown location)",
                Price = o.Price,
                VolumeRemain = o.VolumeRemain,
                VolumeTotal = o.VolumeTotal
            };

        return await query
            .Take(take)
            .ToListAsync();
    }
}
