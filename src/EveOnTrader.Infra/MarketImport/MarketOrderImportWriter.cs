using EveOnTrader.Core.MarketImport;
using EveOnTrader.Core.Models;
using EveOnTrader.Infra.Data;
using EveOnTrader.Infra.Esi.Models;
using Microsoft.EntityFrameworkCore;

namespace EveOnTrader.Infra.MarketImport;

// MarketOrderImportWriter handles DB deletes/inserts for imported market orders.
public class MarketOrderImportWriter
{
    private readonly AppDbContext _db;

    // Creates writer with database access.
    public MarketOrderImportWriter(AppDbContext db)
    {
        _db = db;
    }

    // Deletes old rows for exact same import scope before fresh data is inserted.
    public async Task<long> DeleteExistingScopeAsync(
        MarketOrderImportSlice slice,
        CancellationToken cancellationToken = default)
    {
        var isBuyOrder = slice.Side == MarketOrderSide.Buy;

        var query = _db.MarketOrders
            .Where(x => x.RegionId == slice.RegionId && x.IsBuyOrder == isBuyOrder);

        if (slice.TypeId.HasValue)
        {
            query = query.Where(x => x.TypeId == slice.TypeId.Value);
        }

        return await query.ExecuteDeleteAsync(cancellationToken);
    }

    // Inserts one page of ESI market rows after deduping already-seen order IDs for current request.
    public async Task<int> InsertPageAsync(
        List<EsiMarketRow> rows,
        MarketOrderImportSlice slice,
        Guid importBatchId,
        DateTime importedAtUtc,
        HashSet<long> seenOrderIds,
        CancellationToken cancellationToken = default)
    {
        var orders = new List<MarketOrder>();

        foreach (var row in rows)
        {
            if (!seenOrderIds.Add(row.OrderId))
            {
                continue;
            }

            orders.Add(MarketOrderNormalizer.Normalize(
                row,
                slice.RegionId,
                importBatchId,
                importedAtUtc));
        }

        if (orders.Count == 0)
        {
            return 0;
        }

        _db.MarketOrders.AddRange(orders);
        await _db.SaveChangesAsync(cancellationToken);
        _db.ChangeTracker.Clear();

        return orders.Count;
    }

    // Runs market-order writes with EF auto-detect disabled, then restores previous setting.
    public async Task<T> WithFastImportSettingsAsync<T>(
        Func<Task<T>> action)
    {
        var oldAutoDetectChangesEnabled = _db.ChangeTracker.AutoDetectChangesEnabled;
        _db.ChangeTracker.AutoDetectChangesEnabled = false;

        try
        {
            return await action();
        }
        finally
        {
            _db.ChangeTracker.AutoDetectChangesEnabled = oldAutoDetectChangesEnabled;
        }
    }
}