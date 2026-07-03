namespace EveOnTrader.Core.MarketImport;

// MarketOrderImportResult summarizes one market import run.
public class MarketOrderImportResult
{
    public Guid ImportBatchId { get; set; }
    public DateTime ImportedAtUtc { get; set; }

    public string SelectionName { get; set; } = "";

    public int RequestCount { get; set; }
    public int NormalizedRequestCount { get; set; }

    public int RegionsInserted { get; set; }
    public int SolarSystemsInserted { get; set; }
    public int MarketLocationsInserted { get; set; }
    public int ItemTypeRefsInserted { get; set; }

    public long DeletedMarketOrderCount { get; set; }
    public long InsertedMarketOrderCount { get; set; }

    public TimeSpan Elapsed { get; set; }

    public List<string> Messages { get; set; } = [];
    public List<string> Errors { get; set; } = [];
}