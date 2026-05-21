namespace EveOnTrader.Core.ReadModels;

// OrderListRow is web read model for showing market order details and import metadata.
public class OrderListRow
{
    public long OrderId { get; set; }
    public bool IsBuyOrder { get; set; }
    public DateTime Issued { get; set; }
    public DateTime ImportedAtUtc { get; set; }
    public Guid ImportBatchId { get; set; }
    public string TypeName { get; set; } = "";
    public string RegionName { get; set; } = "";
    public string SystemName { get; set; } = "";
    public string LocationName { get; set; } = "";
    public decimal Price { get; set; }
    public long VolumeRemain { get; set; }
    public long VolumeTotal { get; set; }
}
