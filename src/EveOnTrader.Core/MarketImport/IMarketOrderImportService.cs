namespace EveOnTrader.Core.MarketImport;

// IMarketOrderImportService imports live market orders from ESI.
public interface IMarketOrderImportService
{
    Task<MarketOrderImportResult> ImportAsync(
        MarketOrderImportRequest request,
        CancellationToken cancellationToken = default);
}