using EveOnTrader.Core.ReadModels;
using EveOnTrader.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace EveOnTrader.Infra.Queries;

public class OrderQueryService
{
    private readonly AppDbContext _db;

    public OrderQueryService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<OrderListRow>> GetLatestSellOrdersAsync(int take = 200)
    {
        var query =
            from o in _db.MarketOrders.AsNoTracking()
            join t in _db.ItemTypeRefs.AsNoTracking()
                on o.TypeId equals t.TypeId into typeRefs
            from t in typeRefs.DefaultIfEmpty()
            orderby o.Issued descending
            select new OrderListRow
            {
                OrderId = o.OrderId,
                LocationId = o.LocationId,
                Issued = o.Issued,
                TypeName = t != null ? t.Name : "(unknown item)",
                Price = o.Price,
                VolumeRemain = o.VolumeRemain,
                VolumeTotal = o.VolumeTotal
            };

        return await query
            .Take(take)
            .ToListAsync();
    }
}