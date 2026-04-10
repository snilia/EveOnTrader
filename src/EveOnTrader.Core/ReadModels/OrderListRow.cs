namespace EveOnTrader.Core.ReadModels;

public class OrderListRow
{
    public long OrderId { get; set; }
    public long LocationId { get; set; }
    public DateTime Issued { get; set; }
    public string TypeName { get; set; } = "";
    public decimal Price { get; set; }
    public int VolumeRemain { get; set; }
    public int VolumeTotal { get; set; }
}