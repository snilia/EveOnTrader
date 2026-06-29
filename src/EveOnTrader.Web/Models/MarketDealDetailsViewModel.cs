namespace EveOnTrader.Web.Models;

// MarketDealDetailsViewModel holds one item route details page.
public class MarketDealDetailsViewModel
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

    public decimal SalesTaxRate { get; set; } = 0.0337m;
    public decimal BrokerFeeRate { get; set; } = 0.012m;

    public List<MarketDealDetailsOrderRowViewModel> SourceSellOrders { get; set; } = [];
    public List<MarketDealDetailsOrderRowViewModel> SourceBuyOrders { get; set; } = [];
    public List<MarketDealDetailsOrderRowViewModel> DestinationSellOrders { get; set; } = [];
    public List<MarketDealDetailsOrderRowViewModel> DestinationBuyOrders { get; set; } = [];
}

// MarketDealDetailsOrderRowViewModel holds one displayed market order row.
public class MarketDealDetailsOrderRowViewModel
{
    public long OrderId { get; set; }
    public decimal Price { get; set; }
    public decimal NetImmediateSellPrice { get; set; }
    public decimal NetSellOrderPrice { get; set; }
    public long VolumeRemain { get; set; }
    public long VolumeTotal { get; set; }
    public long MinVolume { get; set; }
    public string Range { get; set; } = "";
    public DateTime Issued { get; set; }

    public long CumulativeVolumeRemain { get; set; }
    public decimal CumulativeVolumeM3 { get; set; }
}