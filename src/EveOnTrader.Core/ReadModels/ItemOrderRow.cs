namespace EveOnTrader.Core.ReadModels;

// ItemOrderRow is one market order row with item, place, and import metadata for route analysis.
public class ItemOrderRow
{
    public long OrderId { get; set; }
    public bool IsBuyOrder { get; set; }
    public DateTime Issued { get; set; }
    public long RegionId { get; set; }
    public long SystemId { get; set; }
    public long LocationId { get; set; }
    public long TypeId { get; set; }
    public decimal Price { get; set; }
    public long VolumeRemain { get; set; }
    public long VolumeTotal { get; set; }
    public long MinVolume { get; set; }
    public long Duration { get; set; }
    public string Range { get; set; } = "";
    public Guid ImportBatchId { get; set; }
    public DateTime ImportedAtUtc { get; set; }
    public decimal UnitVolumeM3 { get; set; }
    public string TypeName { get; set; } = "";
    public string RegionName { get; set; } = "";
    public string SystemName { get; set; } = "";
    public string LocationName { get; set; } = "";
}
