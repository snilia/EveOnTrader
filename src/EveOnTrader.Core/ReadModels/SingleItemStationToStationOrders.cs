namespace EveOnTrader.Core.ReadModels;

// SingleItemStationToStationOrders holds all order books for one item between two stations.
public class SingleItemStationToStationOrders
{
    public long TypeId { get; set; }
    public string TypeName { get; set; } = "";
    public decimal UnitVolumeM3 { get; set; }

    public long SourceLocationId { get; set; }
    public string SourceLocationName { get; set; } = "";
    public long SourceRegionId { get; set; }
    public string SourceRegionName { get; set; } = "";

    public long DestinationLocationId { get; set; }
    public string DestinationLocationName { get; set; } = "";
    public long DestinationRegionId { get; set; }
    public string DestinationRegionName { get; set; } = "";

    public List<ItemOrderRow> SourceSellOrders { get; set; } = [];
    public List<ItemOrderRow> SourceBuyOrders { get; set; } = [];
    public List<ItemOrderRow> DestinationSellOrders { get; set; } = [];
    public List<ItemOrderRow> DestinationBuyOrders { get; set; } = [];
}