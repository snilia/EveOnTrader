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
    private const int BatchSize = 500;

    private readonly AppDbContext _db;
    private readonly EsiClient _esiClient;

    // Creates sync service with DB access and shared ESI client.
    public ItemTypeNameSyncService(AppDbContext db, EsiClient esiClient)
    {
        _db = db;
        _esiClient = esiClient;
    }

    // Finds missing item type IDs from MarketOrders, resolves names from ESI, and stores them in ItemTypeRefs.
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

        Console.WriteLine($"Found {missingTypeIds.Count:n0} missing item type IDs. Resolving names from ESI...");

        var inserted = 0;

        foreach (var batch in Batch(missingTypeIds, BatchSize))
        {
            using var resp = await _esiClient.PostAsJsonAsync(
                "universe/names/?datasource=tranquility",
                batch,
                $"item type names batch ({batch.Count:n0} ids)");

            var results = await resp.Content.ReadFromJsonAsync<List<UniverseNameResult>>()
                         ?? new List<UniverseNameResult>();

            var refsToInsert = results
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .Select(x => new ItemTypeRef
                {
                    TypeId = x.Id,
                    Name = x.Name
                })
                .ToList();

            if (refsToInsert.Count > 0)
            {
                _db.ItemTypeRefs.AddRange(refsToInsert);
                await _db.SaveChangesAsync();
                _db.ChangeTracker.Clear();

                inserted += refsToInsert.Count;
            }

            Console.WriteLine($"Resolved {inserted:n0}/{missingTypeIds.Count:n0} item type names...");
        }

        Console.WriteLine($"Inserted {inserted:n0} new ItemTypeRef rows.");
        return inserted;
    }

    // Splits list of IDs into fixed-size batches for batched ESI requests.
    private static IEnumerable<List<long>> Batch(List<long> source, int batchSize)
    {
        for (var i = 0; i < source.Count; i += batchSize)
        {
            yield return source.Skip(i).Take(batchSize).ToList();
        }
    }
}
