using EveOnTrader.Core.MarketImport;
using EveOnTrader.Core.Models;
using EveOnTrader.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace EveOnTrader.Infra.MarketImport;

// RegionCatalogQuery returns available regions for Worker/Web import UI.
public class RegionCatalogQuery : IRegionCatalogQuery
{
    private readonly AppDbContext _db;
    private readonly UniverseReferenceSyncService _universeReferenceSyncService;

    // Creates region catalog query with DB access and universe sync.
    public RegionCatalogQuery(
        AppDbContext db,
        UniverseReferenceSyncService universeReferenceSyncService)
    {
        _db = db;
        _universeReferenceSyncService = universeReferenceSyncService;
    }

    // Ensures region refs exist, then returns regions sorted by name.
    public async Task<List<Region>> GetRegionsAsync(CancellationToken cancellationToken = default)
    {
        await _db.Database.EnsureCreatedAsync(cancellationToken);
        await _universeReferenceSyncService.SyncRegionsAsync(cancellationToken);

        return await _db.Regions
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);
    }
}