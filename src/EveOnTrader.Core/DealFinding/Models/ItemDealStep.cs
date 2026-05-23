namespace EveOnTrader.Core.DealFinding.Models;

// ItemDealStep is one cumulative stop-point for one item's matched route trade.
public class ItemDealStep
{
    public int StepNumber { get; set; }

    public long LastChunkUnits { get; set; }
    public decimal LastChunkBuyCost { get; set; }
    public decimal LastChunkSellRevenue { get; set; }
    public decimal LastChunkProfit { get; set; }
    public decimal LastChunkVolumeM3 { get; set; }
    public decimal LastChunkRoi { get; set; }
    public decimal LastChunkProfitPerVolumeM3 { get; set; }
    public decimal LastChunkProfitPerJump { get; set; }

    public long TotalUnits { get; set; }
    public decimal TotalBuyCost { get; set; }
    public decimal TotalSellRevenue { get; set; }
    public decimal TotalProfit { get; set; }
    public decimal TotalVolumeM3 { get; set; }
    public decimal TotalRoi { get; set; }
    public decimal TotalProfitPerVolumeM3 { get; set; }
    public decimal TotalProfitPerJump { get; set; }
}
