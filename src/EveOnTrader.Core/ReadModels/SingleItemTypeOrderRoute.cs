namespace EveOnTrader.Core.ReadModels;

// SingleItemTypeOrderRoute holds all source sells and destination buys for one item type on one route.
public class SingleItemTypeOrderRoute
{
    public long TypeId { get; set; }
    public string TypeName { get; set; } = "";
    public List<ItemOrderRow> SourceSellOrders { get; set; } = [];
    public List<ItemOrderRow> DestinationBuyOrders { get; set; } = [];
}
