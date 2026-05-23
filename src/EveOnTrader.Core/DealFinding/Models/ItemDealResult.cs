namespace EveOnTrader.Core.DealFinding.Models;

// ItemDealResult holds all cumulative deal steps for one item type on one route.
public class ItemDealResult
{
    public long TypeId { get; set; }
    public string TypeName { get; set; } = "";
    public decimal UnitVolumeM3 { get; set; }

    public int SourceSellOrderCount { get; set; }
    public int DestinationBuyOrderCount { get; set; }

    public decimal? BestSourceSellPrice { get; set; }
    public decimal? BestDestinationBuyPrice { get; set; }

    public List<ItemDealStep> Steps { get; set; } = [];
}
