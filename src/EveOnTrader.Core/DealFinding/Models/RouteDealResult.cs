namespace EveOnTrader.Core.DealFinding.Models;

// RouteDealResult holds all per-item deal results for one source-to-destination route.
public class RouteDealResult
{
    public long? SourceRegionId { get; set; }
    public long? SourceLocationId { get; set; }
    public long DestinationLocationId { get; set; }
    public DateTime? ImportedAfterUtc { get; set; }
    public int JumpCount { get; set; } = 1;
    public List<ItemDealResult> Items { get; set; } = [];
}
