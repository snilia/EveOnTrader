using EveOnTrader.Core.ReadModels;

namespace EveOnTrader.Core.DealFinding.Services;

// ISingleItemStationToStationQuery loads all buy and sell orders for one item between two stations.
public interface ISingleItemStationToStationQuery
{
    // GetOrdersAsync returns source/destination buy and sell orders for one item.
    Task<SingleItemStationToStationOrders> GetOrdersAsync(
        long typeId,
        long sourceLocationId,
        long destinationLocationId,
        DateTime? importedAfterUtc = null);
}