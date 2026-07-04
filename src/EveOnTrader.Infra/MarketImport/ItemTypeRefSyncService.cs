using EveOnTrader.Core.Models;
using EveOnTrader.Infra.Data;
using EveOnTrader.Infra.Esi;
using EveOnTrader.Infra.Esi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EveOnTrader.Infra.MarketImport;

// ItemTypeRefSyncService fills ItemTypeRefs for imported market order type IDs.
public class ItemTypeRefSyncService
{
    private readonly AppDbContext _db;
    private readonly EsiUniverseClient _esiUniverseClient;
    private readonly ILogger<ItemTypeRefSyncService> _logger;

    // Creates sync service with DB access, ESI universe client, and logger.
    public ItemTypeRefSyncService(
        AppDbContext db,
        EsiUniverseClient esiUniverseClient,
        ILogger<ItemTypeRefSyncService> logger)
    {
        _db = db;
        _esiUniverseClient = esiUniverseClient;
        _logger = logger;
    }

    // Finds missing item type IDs from MarketOrders, resolves name/volume from ESI, and stores them.
    public async Task<int> SyncItemTypeRefsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Checking for missing item type names...");

        var marketTypeIds = await _db.MarketOrders
            .Select(x => x.TypeId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var existingTypeIds = await _db.ItemTypeRefs
            .Select(x => x.TypeId)
            .ToHashSetAsync(cancellationToken);

        var missingTypeIds = marketTypeIds
            .Where(id => !existingTypeIds.Contains(id))
            .OrderBy(id => id)
            .ToList();

        if (missingTypeIds.Count == 0)
        {
            _logger.LogInformation("No missing item type names.");
            return 0;
        }

        _logger.LogInformation(
            "Found {MissingCount:n0} missing item type IDs. Resolving details from ESI...",
            missingTypeIds.Count);

        var refsToInsert = new List<ItemTypeRef>();
        var processed = 0;

        foreach (var typeId in missingTypeIds)
        {
            var typeResult = await _esiUniverseClient.GetItemTypeAsync(typeId, cancellationToken);

            if (typeResult is null || string.IsNullOrWhiteSpace(typeResult.Name))
            {
                continue;
            }

            refsToInsert.Add(new ItemTypeRef
            {
                TypeId = typeId,
                Name = typeResult.Name,
                VolumeM3 = GetMarketVolumeM3(typeResult)
            });

            processed++;

            if (processed % 100 == 0 || processed == missingTypeIds.Count)
            {
                _logger.LogInformation(
                    "Resolved {Processed:n0}/{Total:n0} item type details...",
                    processed,
                    missingTypeIds.Count);
            }
        }

        if (refsToInsert.Count == 0)
        {
            _logger.LogInformation("ESI returned no new item type refs.");
            return 0;
        }

        _db.ItemTypeRefs.AddRange(refsToInsert);
        await _db.SaveChangesAsync(cancellationToken);
        _db.ChangeTracker.Clear();

        _logger.LogInformation("Inserted {InsertedCount:n0} new ItemTypeRef rows.", refsToInsert.Count);
        return refsToInsert.Count;
    }

    // GetMarketVolumeM3 returns packaged volume when ESI provides it, otherwise normal type volume.
    private static decimal GetMarketVolumeM3(EsiItemType typeResult)
    {
        return typeResult.PackagedVolumeM3 > 0m
            ? typeResult.PackagedVolumeM3
            : typeResult.VolumeM3;
    }
}