using System.Net.Http.Headers;
using System.Net.Http.Json;
using EveOnTrader.Core.Models;
using EveOnTrader.Infra.Data;
using EveOnTrader.Worker.Models;
using Microsoft.EntityFrameworkCore;

namespace EveOnTrader.Worker.Services;


//UniverseNameSyncService is a worker-side sync service that finds item IDs missing from ItemTypeRefs, 
//resolves their names from ESI in batches, and saves them into the database.
public class UniverseNameSyncService
{
    private const string CompatibilityDate = "2025-12-16";
    private const int BatchSize = 500;

    private readonly AppDbContext _db;

    public UniverseNameSyncService(AppDbContext db)
    {
        _db = db;
    }

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

        using var http = CreateHttpClient();

        var inserted = 0;

        foreach (var batch in Batch(missingTypeIds, BatchSize))
        {
            using var resp = await http.PostAsJsonAsync(
                "universe/names/?datasource=tranquility",
                batch);

            resp.EnsureSuccessStatusCode();

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

    private static HttpClient CreateHttpClient()
    {
        var http = new HttpClient
        {
            BaseAddress = new Uri("https://esi.evetech.net/latest/")
        };

        http.DefaultRequestHeaders.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json"));

        http.DefaultRequestHeaders.TryAddWithoutValidation(
            "X-Compatibility-Date",
            CompatibilityDate);

        http.DefaultRequestHeaders.UserAgent.ParseAdd("EveOnTrader.Worker/1.0");

        return http;
    }

    private static IEnumerable<List<long>> Batch(List<long> source, int batchSize)
    {
        for (var i = 0; i < source.Count; i += batchSize)
        {
            yield return source.Skip(i).Take(batchSize).ToList();
        }
    }
}