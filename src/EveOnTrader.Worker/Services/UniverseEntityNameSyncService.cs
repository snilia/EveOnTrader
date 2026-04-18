using System.Net.Http.Headers;
using System.Net.Http.Json;
using EveOnTrader.Core.Models;
using EveOnTrader.Infra.Data;
using EveOnTrader.Worker.Models;
using Microsoft.EntityFrameworkCore;

namespace EveOnTrader.Worker.Services;

// Finds missing region/system/location IDs from MarketOrders,
// resolves names from ESI, saves them into UniverseEntityRefs.
public class UniverseEntityNameSyncService
{
    private const string CompatibilityDate = "2025-12-16";
    private const int BatchSize = 500;

    private readonly AppDbContext _db;

    public UniverseEntityNameSyncService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<int> SyncUniverseEntityRefsAsync()
    {
        Console.WriteLine("Checking for missing universe entity names...");

        var regionIds = await _db.MarketOrders
            .Select(x => (long)x.RegionId)
            .Distinct()
            .ToListAsync();

        var systemIds = await _db.MarketOrders
            .Select(x => x.SystemId)
            .Distinct()
            .ToListAsync();

        var locationIds = await _db.MarketOrders
            .Select(x => x.LocationId)
            .Distinct()
            .ToListAsync();

        var marketEntityIds = regionIds
            .Concat(systemIds)
            .Concat(locationIds)
            .Where(id => id > 0)
            .Distinct()
            .OrderBy(id => id)
            .ToList();

        var existingEntityIds = await _db.UniverseEntityRefs
            .Select(x => x.EntityId)
            .ToHashSetAsync();

        var missingEntityIds = marketEntityIds
            .Where(id => !existingEntityIds.Contains(id))
            .ToList();

        if (missingEntityIds.Count == 0)
        {
            Console.WriteLine("No missing universe entity names.");
            return 0;
        }

        Console.WriteLine($"Found {missingEntityIds.Count:n0} missing universe entity IDs. Resolving names from ESI...");

        using var http = CreateHttpClient();

        var inserted = 0;

        foreach (var batch in Batch(missingEntityIds, BatchSize))
        {
            using var resp = await http.PostAsJsonAsync(
                "universe/names/?datasource=tranquility",
                batch);

            resp.EnsureSuccessStatusCode();

            var results = await resp.Content.ReadFromJsonAsync<List<UniverseNameResult>>()
                         ?? new List<UniverseNameResult>();

            var refsToInsert = results
                .Where(x => x.Id > 0 && !string.IsNullOrWhiteSpace(x.Name))
                .Select(x => new UniverseEntityRef
                {
                    EntityId = x.Id,
                    Category = x.Category,
                    Name = x.Name
                })
                .ToList();

            if (refsToInsert.Count > 0)
            {
                _db.UniverseEntityRefs.AddRange(refsToInsert);
                await _db.SaveChangesAsync();
                _db.ChangeTracker.Clear();

                inserted += refsToInsert.Count;
            }

            Console.WriteLine($"Resolved {inserted:n0}/{missingEntityIds.Count:n0} universe entity names...");
        }

        Console.WriteLine($"Inserted {inserted:n0} new UniverseEntityRef rows.");
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
