namespace EveOnTrader.Core.Models;

// ItemTypeRef stores item type metadata used by queries and deal finding.
public class ItemTypeRef
{
    public long TypeId { get; set; }
    public string Name { get; set; } = "";
    public decimal VolumeM3 { get; set; }
}
