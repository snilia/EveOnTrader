namespace EveOnTrader.Core.DealFinding.Models;

// RouteSecurityLimit controls how dangerous route is allowed to be.
public enum RouteSecurityLimit
{
    HighSecOnly = 0,
    LowSecAllowed = 1,
    NullSecAllowed = 2
}

// DealFinderOptions holds optional limits and filters for deal search.
public class DealFinderOptions
{
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
