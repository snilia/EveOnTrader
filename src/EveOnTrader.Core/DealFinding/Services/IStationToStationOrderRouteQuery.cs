using EveOnTrader.Core.ReadModels;

namespace EveOnTrader.Core.DealFinding.Services;

// IStationToStationOrderRouteQuery loads source sell orders and destination buy orders for one station-to-station route.
public interface IStationToStationOrderRouteQuery
{
    // GetAllItemTypesOrderRouteAsync returns grouped item routes for one source location and one destination location.
    // SourceSellOrders must be sorted by cheapest price first.
    // DestinationBuyOrders must be sorted by highest price first.
    Task<AllItemTypesOrderRoute> GetAllItemTypesOrderRouteAsync(
        long sourceLocationId,
        long destinationLocationId,
        DateTime? importedAfterUtc = null);
}