namespace EveOnTrader.Worker.Models;

// MarketImportOptions groups one named set of order import requests for a worker run.
public class MarketImportOptions
{
    public string SelectionName { get; set; } = "";
    public IReadOnlyList<OrderImportRequest> Requests { get; set; } = Array.Empty<OrderImportRequest>();
}
