namespace EveOnTrader.Core.ReadModels;

// MultiLocationMarketOrderRows holds all market order rows needed for a multi-location deal search.
public class MultiLocationMarketOrderRows
{
    public List<ItemOrderRow> SourceSellOrders { get; set; } = [];
    public List<ItemOrderRow> DestinationBuyOrders { get; set; } = [];
}