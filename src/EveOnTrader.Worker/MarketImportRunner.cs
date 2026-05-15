using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EveOnTrader.Core.Models;
using EveOnTrader.Infra.Data;
using EveOnTrader.Worker.Models;
using EveOnTrader.Worker.Services;
using Microsoft.EntityFrameworkCore;

namespace EveOnTrader.Worker;

public class MarketImportRunner
{
    private const string OrderType = "sell";
    private const string CompatibilityDate = "2025-12-16";

    private readonly AppDbContext _db;
    private readonly ItemTypeNameSyncService _itemTypeNameSyncService;
    private readonly UniverseSyncService _universeSyncService;

    public MarketImportRunner(
        AppDbContext db,
        ItemTypeNameSyncService itemTypeNameSyncService,
        UniverseSyncService universeSyncService)
    {
        _db = db;
        _itemTypeNameSyncService = itemTypeNameSyncService;
        _universeSyncService = universeSyncService;
    }

    public async Task RunAsync(string dbPath, MarketImportOptions options)
    {
        Console.WriteLine($"DB Path: {dbPath}");
        Console.WriteLine("Ensuring DB exists...");
        await _db.Database.EnsureCreatedAsync();

        var sw = Stopwatch.StartNew();

        Console.WriteLine($"Selection: {options.SelectionName}");
        Console.WriteLine($"Regions selected: {options.RegionIds.Count:n0}");

        var (regionsInserted, solarSystemsInserted) = await _universeSyncService.SyncAsync();
        var totalInserted = await ImportMarketOrdersAsync(options.RegionIds);
        var marketLocationsInserted = await _universeSyncService.SyncMarketLocationsAsync();
        var typeRefsInserted = await _itemTypeNameSyncService.SyncItemTypeRefsAsync();

        sw.Stop();

        Console.WriteLine();
        Console.WriteLine("DONE.");
        Console.WriteLine($"Selection: {options.SelectionName}");
        Console.WriteLine($"Imported {options.RegionIds.Count:n0} regions.");
        Console.WriteLine($"Inserted {regionsInserted:n0} new regions.");
        Console.WriteLine($"Inserted {solarSystemsInserted:n0} new solar systems.");
        Console.WriteLine($"Inserted {totalInserted:n0} {OrderType} orders.");
        Console.WriteLine($"Inserted {marketLocationsInserted:n0} new market locations.");
        Console.WriteLine($"Inserted {typeRefsInserted:n0} new item type names.");
        Console.WriteLine($"Total elapsed: {sw.Elapsed}.");
    }

    private async Task<long> ImportMarketOrdersAsync(IReadOnlyList<long> regionIds)
    {
        Console.WriteLine("Clearing MarketOrders (fresh snapshot)...");
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM MarketOrders");

        //EF's auto change detection off, helps speed
        _db.ChangeTracker.AutoDetectChangesEnabled = false;

        using var http = CreateHttpClient();

        long totalInserted = 0;
        var seenOrderIds = new HashSet<long>();

        foreach (var regionId in regionIds)
        {
            long totalPages = 1;

            Console.WriteLine($"Importing region {regionId}...");

            for (var page = 1; page <= totalPages; page++)
            {
                var url = $"markets/{regionId}/orders/?order_type={OrderType}&datasource=tranquility&page={page}";
                using var resp = await http.GetAsync(url);

                resp.EnsureSuccessStatusCode();

                totalPages = long.Parse(resp.Headers.GetValues("X-Pages").First());

                var orders = await resp.Content.ReadFromJsonAsync<List<MarketOrder>>()
                             ?? new List<MarketOrder>();

                foreach (var o in orders)
                {
                    o.RegionId = regionId;
                }

                var newOrders = orders
                    .Where(o => seenOrderIds.Add(o.OrderId))
                    .ToList();

                _db.MarketOrders.AddRange(newOrders);
                await _db.SaveChangesAsync();

                totalInserted += newOrders.Count;

                //cleares tracked entities from EF memory, helps with big imports
                _db.ChangeTracker.Clear();

                Console.WriteLine(
                    $"Region {regionId} page {page}/{totalPages}: inserted {newOrders.Count:n0} (raw {orders.Count:n0}, total {totalInserted:n0})");
            }
        }

        return totalInserted;
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
}
