namespace EveOnTrader.Core.ReadModels;

public class OrderListRow
{
    public long OrderId { get; set; }
    public DateTime Issued { get; set; }
    public string TypeName { get; set; } = "";
    public string RegionName { get; set; } = "";
    public string SystemName { get; set; } = "";
    public string LocationName { get; set; } = "";
    public decimal Price { get; set; }
    public long VolumeRemain { get; set; }
    public long VolumeTotal { get; set; }
}