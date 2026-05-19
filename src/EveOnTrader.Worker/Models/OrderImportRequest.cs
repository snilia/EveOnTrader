namespace EveOnTrader.Worker.Models;

// OrderImportRequest defines one exact market-order slice to download/refresh from ESI.
public class OrderImportRequest
{
    public long RegionId { get; set; }
    public bool IsBuyOrder { get; set; }
    public long? TypeId { get; set; }
}
