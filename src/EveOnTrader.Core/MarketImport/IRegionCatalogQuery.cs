using EveOnTrader.Core.Models;

namespace EveOnTrader.Core.MarketImport;

// IRegionCatalogQuery returns valid EVE regions for import UI/prompt.
public interface IRegionCatalogQuery
{
    Task<List<Region>> GetRegionsAsync(CancellationToken cancellationToken = default);
}