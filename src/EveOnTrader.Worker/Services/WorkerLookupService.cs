using EveOnTrader.Core.Models;
using EveOnTrader.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace EveOnTrader.Worker.Services;

//This service is used to return stuff from DB
public class WorkerLookupService
{
    private readonly AppDbContext _db;
    private readonly UniverseSyncService _universeSyncService;

    public WorkerLookupService(
        AppDbContext db,
        UniverseSyncService universeSyncService)
    {
        _db = db;
        _universeSyncService = universeSyncService;
    }

    //returns all regions from DB, if DB is empty it will sync with ESI and return the regions
    public async Task<List<Region>> GetAvailableRegionsAsync()
    {
        await _db.Database.EnsureCreatedAsync();
        await _universeSyncService.SyncRegionsAsync();

        return await _db.Regions
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync();
    }
}
