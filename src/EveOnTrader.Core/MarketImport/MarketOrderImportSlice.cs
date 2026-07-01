namespace EveOnTrader.Core.MarketImport;

// MarketOrderSide identifies which ESI market order side to import.
public enum MarketOrderSide
{
    Sell = 0,
    Buy = 1
}

// MarketOrderImportSlice defines one exact market-order scope to refresh.
public class MarketOrderImportSlice
{
    public long RegionId { get; set; }
    public MarketOrderSide Side { get; set; }
    public long? TypeId { get; set; }
}