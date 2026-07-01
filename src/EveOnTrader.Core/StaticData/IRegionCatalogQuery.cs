using EveOnTrader.Core.Models;

namespace EveOnTrader.Core.StaticData;

// IRegionCatalogQuery returns known regions from static data.
public interface IRegionCatalogQuery
{
    Task<List<Region>> GetRegionsAsync(
        CancellationToken cancellationToken = default);
}