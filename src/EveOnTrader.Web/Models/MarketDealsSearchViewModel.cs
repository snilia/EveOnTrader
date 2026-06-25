using EveOnTrader.Core.DealFinding.Models;

namespace EveOnTrader.Web.Models;

// MarketDealsSearchInputViewModel holds user-entered search inputs and deal-finder options.
public class MarketDealsSearchInputViewModel
{
    public string SellStationIdsText { get; set; } = "";
    public string SellRegionIdsText { get; set; } = "";
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

// MarketDealsSearchResultViewModel holds search results and summary counts.
public class MarketDealsSearchResultViewModel
{
    public List<MarketDealsSearchRowViewModel> Rows { get; set; } = [];

    public int SellStationCount { get; set; }
    public int SellRegionCount { get; set; }
    public int BuyStationCount { get; set; }
}

// MarketDealsSearchRowViewModel holds one displayed deal row.
public class MarketDealsSearchRowViewModel
{
    public long TypeId { get; set; }
    public string TypeName { get; set; } = "";

    public long? SellRegionId { get; set; }
    public string SellRegionName { get; set; } = "";
    public long? SellStationId { get; set; }
    public string SellStationName { get; set; } = "";

    public long? BuyRegionId { get; set; }
    public string BuyRegionName { get; set; } = "";
    public long BuyStationId { get; set; }
    public string BuyStationName { get; set; } = "";

    public int JumpCount { get; set; }

    public int StepCount { get; set; }
    public long TotalUnits { get; set; }
    public decimal TotalBuyCost { get; set; }
    public decimal TotalSellRevenue { get; set; }
    public decimal TotalProfit { get; set; }
    public decimal TotalRoi { get; set; }
    public decimal TotalVolumeM3 { get; set; }
    public decimal TotalProfitPerVolumeM3 { get; set; }
    public decimal TotalProfitPerJump { get; set; }

    public decimal? BestSellPrice { get; set; }
    public decimal? BestBuyPrice { get; set; }
}

// MarketDealsSearchViewModel holds full page state.
public class MarketDealsSearchViewModel
{
    public MarketDealsSearchInputViewModel Input { get; set; } = new();
    public MarketDealsSearchResultViewModel Result { get; set; } = new();
    public string? ErrorMessage { get; set; }
}