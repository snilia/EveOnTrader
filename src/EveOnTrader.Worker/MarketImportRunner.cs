using System.Diagnostics;
using System.Net.Http.Json;
using EveOnTrader.Core.Models;
using EveOnTrader.Infra.Data;
using EveOnTrader.Worker.Models;
using EveOnTrader.Worker.Services;
using Microsoft.EntityFrameworkCore;

namespace EveOnTrader.Worker;

// MarketImportRunner coordinates one worker import run: seed references, import selected order slices, then resolve related lookup data.
public class MarketImportRunner
{
    private readonly AppDbContext _db;
    private readonly EsiClient _esiClient;
    private readonly ItemTypeNameSyncService _itemTypeNameSyncService;
    private readonly UniverseSyncService _universeSyncService;

    // Creates runner with database access, shared ESI client, and supporting sync services.
    public MarketImportRunner(
        AppDbContext db,
        EsiClient esiClient,
        ItemTypeNameSyncService itemTypeNameSyncService,
        UniverseSyncService universeSyncService)
    {
        _db = db;
        _esiClient = esiClient;
        _itemTypeNameSyncService = itemTypeNameSyncService;
        _universeSyncService = universeSyncService;
    }

    // Runs full worker flow for selected import requests: reference sync, market import, location sync, and item type sync.
    public async Task RunAsync(string dbPath, MarketImportOptions options)
    {
        Console.WriteLine($"DB Path: {dbPath}");
        Console.WriteLine("Ensuring DB exists...");
        await _db.Database.EnsureCreatedAsync();

        var sw = Stopwatch.StartNew();
        var normalizedRequests = NormalizeRequests(options.Requests);

        Console.WriteLine($"Selection: {options.SelectionName}");
        Console.WriteLine($"Requests selected: {normalizedRequests.Count:n0}");
        Console.WriteLine($"Sell requests: {normalizedRequests.Count(x => !x.IsBuyOrder):n0}");
        Console.WriteLine($"Buy requests: {normalizedRequests.Count(x => x.IsBuyOrder):n0}");

        ValidateNoOverlappingRequests(normalizedRequests);

        var (regionsInserted, solarSystemsInserted) = await _universeSyncService.SyncAsync();
        var totalInserted = await ImportMarketOrdersAsync(normalizedRequests);
        var marketLocationsInserted = await _universeSyncService.SyncMarketLocationsAsync();
        var typeRefsInserted = await _itemTypeNameSyncService.SyncItemTypeRefsAsync();

        sw.Stop();

        Console.WriteLine();
        Console.WriteLine("DONE.");
        Console.WriteLine($"Selection: {options.SelectionName}");
        Console.WriteLine($"Imported {normalizedRequests.Count:n0} request slices.");
        Console.WriteLine($"Inserted {regionsInserted:n0} new regions.");
        Console.WriteLine($"Inserted {solarSystemsInserted:n0} new solar systems.");
        Console.WriteLine($"Inserted {totalInserted:n0} market orders.");
        Console.WriteLine($"Inserted {marketLocationsInserted:n0} new market locations.");
        Console.WriteLine($"Inserted {typeRefsInserted:n0} new item type names.");
        Console.WriteLine($"Total elapsed: {sw.Elapsed}.");
    }

    // Imports all selected order request slices and stamps each row with one batch ID and import time.
    private async Task<long> ImportMarketOrdersAsync(IReadOnlyList<OrderImportRequest> requests)
    {
        //EF's auto change detection off, helps speed
        _db.ChangeTracker.AutoDetectChangesEnabled = false;

        var importBatchId = Guid.NewGuid();
        var importedAtUtc = DateTime.UtcNow;
        long totalInserted = 0;

        foreach (var request in requests)
        {
            totalInserted += await ImportRequestAsync(request, importBatchId, importedAtUtc);
        }

        return totalInserted;
    }

    // Refreshes one exact import scope by deleting old rows for same scope and inserting fresh rows from ESI.
    private async Task<long> ImportRequestAsync(OrderImportRequest request, Guid importBatchId, DateTime importedAtUtc)
    {
        var requestLabel = DescribeRequest(request);

        Console.WriteLine($"Refreshing {requestLabel}...");
        await DeleteExistingScopeAsync(request);

        long totalPages = 1;
        long insertedForRequest = 0;
        var seenOrderIds = new HashSet<long>();

        for (var page = 1; page <= totalPages; page++)
        {
            var url = BuildMarketOrdersUrl(request, page);
            using var resp = await _esiClient.GetAsync(url, $"{requestLabel} page {page}");

            totalPages = long.Parse(resp.Headers.GetValues("X-Pages").First());

            var orders = await resp.Content.ReadFromJsonAsync<List<MarketOrder>>()
                         ?? new List<MarketOrder>();

            foreach (var o in orders)
            {
                o.RegionId = request.RegionId;
                o.ImportBatchId = importBatchId;
                o.ImportedAtUtc = importedAtUtc;
            }

            var newOrders = orders
                .Where(o => seenOrderIds.Add(o.OrderId))
                .ToList();

            _db.MarketOrders.AddRange(newOrders);
            await _db.SaveChangesAsync();

            insertedForRequest += newOrders.Count;

            //cleares tracked entities from EF memory, helps with big imports
            _db.ChangeTracker.Clear();

            Console.WriteLine(
                $"{requestLabel} page {page}/{totalPages}: inserted {newOrders.Count:n0} (raw {orders.Count:n0}, total {insertedForRequest:n0})");
        }

        return insertedForRequest;
    }


    // Deletes old rows for exact same import scope before fresh data for that scope is inserted.
    private async Task DeleteExistingScopeAsync(OrderImportRequest request)
    {
        var query = _db.MarketOrders
            .Where(x => x.RegionId == request.RegionId && x.IsBuyOrder == request.IsBuyOrder);

        if (request.TypeId.HasValue)
        {
            query = query.Where(x => x.TypeId == request.TypeId.Value);
        }

        await query.ExecuteDeleteAsync();
    }

    // Builds ESI market-orders URL for one request slice and page number.
    private static string BuildMarketOrdersUrl(OrderImportRequest request, int page)
    {
        var orderType = request.IsBuyOrder ? "buy" : "sell";
        var url = $"markets/{request.RegionId}/orders/?order_type={orderType}&datasource=tranquility&page={page}";

        if (request.TypeId.HasValue)
        {
            url += $"&type_id={request.TypeId.Value}";
        }

        return url;
    }

    // Returns readable label for logs from one import request.
    private static string DescribeRequest(OrderImportRequest request)
    {
        var orderType = request.IsBuyOrder ? "buy" : "sell";

        if (request.TypeId.HasValue)
        {
            return $"Region {request.RegionId} {orderType} type {request.TypeId.Value}";
        }

        return $"Region {request.RegionId} {orderType} all-items";
    }

    // Removes exact duplicate requests so one scope is not imported twice in same run.
    private static IReadOnlyList<OrderImportRequest> NormalizeRequests(IReadOnlyList<OrderImportRequest> requests)
    {
        return requests
            .GroupBy(x => new { x.RegionId, x.IsBuyOrder, x.TypeId })
            .Select(x => x.First())
            .OrderBy(x => x.RegionId)
            .ThenBy(x => x.IsBuyOrder)
            .ThenBy(x => x.TypeId)
            .ToList();
    }

    // Blocks overlapping requests like full-region plus one-item same-side imports in same run.
    private static void ValidateNoOverlappingRequests(IReadOnlyList<OrderImportRequest> requests)
    {
        var groups = requests
            .GroupBy(x => new { x.RegionId, x.IsBuyOrder });

        foreach (var group in groups)
        {
            if (group.Any(x => x.TypeId is null) && group.Count() > 1)
            {
                var orderType = group.Key.IsBuyOrder ? "buy" : "sell";

                throw new InvalidOperationException(
                    $"Overlapping import requests not supported for region {group.Key.RegionId} {orderType}. Use either full-region or per-item requests, not both in same run.");
            }
        }
    }
}
