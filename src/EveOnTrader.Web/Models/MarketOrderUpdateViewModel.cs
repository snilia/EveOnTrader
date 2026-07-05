using EveOnTrader.Core.MarketImport;
using EveOnTrader.Core.Models;

namespace EveOnTrader.Web.Models;

// MarketOrderUpdateViewModel holds market order update form state and result.
public class MarketOrderUpdateViewModel
{
    public string SourceRegionIdsText { get; set; } = "";
    public string DestinationRegionIdsText { get; set; } = "";

    public List<Region> AvailableRegions { get; set; } = [];

    public MarketOrderImportResult? Result { get; set; }
    public string? ErrorMessage { get; set; }
}