using System.Net.Http.Json;
using EveOnTrader.Core.Models;
using EveOnTrader.Infra.Data;
using EveOnTrader.Worker.Models;
using Microsoft.EntityFrameworkCore;

namespace EveOnTrader.Worker.Services;


//ItemTypeNameSyncService is a worker-side sync service that finds item IDs missing from ItemTypeRefs, 
//resolves their names from ESI in batches, and saves them into the database.
public class ItemTypeNameSyncService
{
    private readonly AppDbContext _db;
    private readonly EsiClient _esiClient;

    // Creates sync service with DB access and shared ESI client.
    public ItemTypeNameSyncService(AppDbContext db, EsiClient esiClient)
    {
        _db = db;
        _esiClient = esiClient;
    }

    // Finds missing item type IDs from MarketOrders, resolves names and volume from ESI, and stores them in ItemTypeRefs.
    public async Task<int> SyncItemTypeRefsAsync()
    {
        Console.WriteLine("Checking for missing item type names...");

        var marketTypeIds = await _db.MarketOrders
            .Select(x => x.TypeId)
            .Distinct()
            .ToListAsync();

        var existingTypeIds = await _db.ItemTypeRefs
            .Select(x => x.TypeId)
            .ToHashSetAsync();

        var missingTypeIds = marketTypeIds
            .Where(id => !existingTypeIds.Contains(id))
            .OrderBy(id => id)
            .ToList();

        if (missingTypeIds.Count == 0)
        {
            Console.WriteLine("No missing item type names.");
            return 0;
        }

        Console.WriteLine($"Found {missingTypeIds.Count:n0} missing item type IDs. Resolving details from ESI...");

        var refsToInsert = new List<ItemTypeRef>();
        var processed = 0;

        foreach (var typeId in missingTypeIds)
        {
            using var resp = await _esiClient.GetAsync(
                $"universe/types/{typeId}/?datasource=tranquility",
                $"item type details {typeId}");

            var typeResult = await resp.Content.ReadFromJsonAsync<UniverseTypeResult>();

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
                Console.WriteLine($"Resolved {processed:n0}/{missingTypeIds.Count:n0} item type details...");
            }
        }

        if (refsToInsert.Count > 0)
        {
            _db.ItemTypeRefs.AddRange(refsToInsert);
            await _db.SaveChangesAsync();
            _db.ChangeTracker.Clear();
        }

        Console.WriteLine($"Inserted {refsToInsert.Count:n0} new ItemTypeRef rows.");
        return refsToInsert.Count;
    }

    // GetMarketVolumeM3 returns packaged volume when ESI provides it, otherwise normal type volume.
    private static decimal GetMarketVolumeM3(UniverseTypeResult typeResult)
    {
        return typeResult.PackagedVolumeM3 > 0m
            ? typeResult.PackagedVolumeM3
            : typeResult.VolumeM3;
    }
}
