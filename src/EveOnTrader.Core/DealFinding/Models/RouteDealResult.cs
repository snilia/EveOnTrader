namespace EveOnTrader.Core.DealFinding.Models;

// RouteDealResult holds all per-item deal results for one source-to-destination route.
public class RouteDealResult
{
    public long? SourceRegionId { get; set; }
    public string SourceRegionName { get; set; } = "";
    public long? SourceLocationId { get; set; }
    public string SourceLocationName { get; set; } = "";

    public long? DestinationRegionId { get; set; }
    public string DestinationRegionName { get; set; } = "";
    public long DestinationLocationId { get; set; }
    public string DestinationLocationName { get; set; } = "";

    public DateTime? ImportedAfterUtc { get; set; }
    public int JumpCount { get; set; } = 1;
    public List<ItemDealResult> Items { get; set; } = [];
}