using EveOnTrader.Core.ReadModels;

namespace EveOnTrader.Core.DealFinding.Services;

// IMultiLocationMarketOrderQuery loads all market order rows needed for a multi-location deal search.
public interface IMultiLocationMarketOrderQuery
{
    // GetMarketOrderRowsAsync returns source sell rows and destination buy rows for all requested locations.
    // SourceSellOrders are sorted by LocationId, TypeId, cheapest price first.
    // DestinationBuyOrders are sorted by LocationId, TypeId, highest price first.
    Task<MultiLocationMarketOrderRows> GetMarketOrderRowsAsync(
        List<long> sourceLocationIds,
        List<long> destinationLocationIds,
        DateTime? importedAfterUtc = null);
}