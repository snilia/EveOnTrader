namespace EveOnTrader.Worker.Models;

public class MarketImportOptions
{
    public string SelectionName { get; set; } = "";
    public IReadOnlyList<long> RegionIds { get; set; } = Array.Empty<long>();
}
