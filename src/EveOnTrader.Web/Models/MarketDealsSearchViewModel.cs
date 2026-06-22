using EveOnTrader.Core.DealFinding.Models;

namespace EveOnTrader.Web.Models;

// MarketDealsSearchInputViewModel holds user-entered search inputs and deal-finder options.
public class MarketDealsSearchInputViewModel
{
    public string SellStationIdsText { get; set; } = "";
    public string BuyStationIdsText { get; set; } = "";

    public decimal? MaxTotalBuyCost { get; set; }
    public decimal? MaxTotalVolumeM3 { get; set; }
    public decimal? MinTotalProfit { get; set; }
    public decimal? MinTotalRoi { get; set; }
    public decimal? MinProfitPerJump { get; set; }
    public int? MaxSteps { get; set; }
    public decimal BrokerFeeRate { get; set; } = 0.012m;
    public decimal SalesTaxRate { get; set; } = 0.0337m;
    public RouteSecurityLimit RouteSecurityLimit { get; set; } = RouteSecurityLimit.NullSecAllowed;
}

// MarketDealsSearchRowViewModel holds one displayed best-deal row for one item on one station-to-station route.
public class MarketDealsSearchRowViewModel
{
    public long SellStationId { get; set; }
    public string SellStationName { get; set; } = "";
    public long? SellRegionId { get; set; }
    public string SellRegionName { get; set; } = "";

    public long BuyStationId { get; set; }
    public string BuyStationName { get; set; } = "";
    public long? BuyRegionId { get; set; }
    public string BuyRegionName { get; set; } = "";

    public int JumpCount { get; set; }

    public long TypeId { get; set; }
    public string TypeName { get; set; } = "";
    public int StepCount { get; set; }
    public long TotalUnits { get; set; }
    public decimal TotalVolumeM3 { get; set; }
    public decimal TotalBuyCost { get; set; }
    public decimal TotalSellRevenue { get; set; }
    public decimal TotalProfit { get; set; }
    public decimal TotalRoi { get; set; }
    public decimal TotalProfitPerVolumeM3 { get; set; }
    public decimal TotalProfitPerJump { get; set; }
    public decimal? BestSourceSellPrice { get; set; }
    public decimal? BestDestinationBuyPrice { get; set; }
}

// MarketDealsSearchResultViewModel holds search status, summary, messages, and displayed deal rows.
public class MarketDealsSearchResultViewModel
{
    public bool HasSearched { get; set; }
    public int SellStationCount { get; set; }
    public int BuyStationCount { get; set; }
    public int RoutePairCount { get; set; }
    public string SearchMessage { get; set; } = "";

    public List<MarketDealsSearchRowViewModel> DealRows { get; set; } = [];
}

// MarketDealsSearchViewModel holds page input and page result separately.
public class MarketDealsSearchViewModel
{
    public MarketDealsSearchInputViewModel Input { get; set; } = new();
    public MarketDealsSearchResultViewModel Result { get; set; } = new();
}