namespace EveOnTrader.Core.MarketImport;

// MarketOrderImportRequest defines one full market import run.
public class MarketOrderImportRequest
{
    public string SelectionName { get; set; } = "";
    public List<MarketOrderImportSlice> Slices { get; set; } = [];
}