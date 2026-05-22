namespace EveOnTrader.Core.ReadModels;

// AllItemTypesOrderRoute holds all grouped item routes for one source-to-destination trade search.
public class AllItemTypesOrderRoute
{
    public long SourceRegionId { get; set; }
    public long DestinationLocationId { get; set; }
    public DateTime? ImportedAfterUtc { get; set; }
    public List<SingleItemTypeOrderRoute> Items { get; set; } = [];
}
