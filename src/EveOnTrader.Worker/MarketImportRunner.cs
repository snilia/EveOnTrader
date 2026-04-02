using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EveOnTrader.Core.Models;
using EveOnTrader.Infra.Data;
using Microsoft.EntityFrameworkCore;

namespace EveOnTrader.Worker;

public class MarketImportRunner
{
    private const int RegionId = 10000002;
    private const string OrderType = "sell";
    private const string CompatibilityDate = "2025-12-16";

    private readonly AppDbContext _db;

    public MarketImportRunner(AppDbContext db)
    {
        _db = db;
    }

    public async Task RunAsync(string dbPath)
    {
        Console.WriteLine($"DB Path: {dbPath}");
        Console.WriteLine("Ensuring DB exists...");
        await _db.Database.EnsureCreatedAsync();

        Console.WriteLine("Clearing MarketOrders (fresh snapshot)...");
        await _db.Database.ExecuteSqlRawAsync("DELETE FROM MarketOrders");

        //EF's auto change detection off, helps speed
        _db.ChangeTracker.AutoDetectChangesEnabled = false;

        using var http = CreateHttpClient();

        var sw = Stopwatch.StartNew();

        long totalInserted = 0;
        long totalPages = 1;
        var seenOrderIds = new HashSet<long>();

        for (var page = 1; page <= totalPages; page++)
        {
            var url = $"markets/{RegionId}/orders/?order_type={OrderType}&datasource=tranquility&page={page}";
            using var resp = await http.GetAsync(url);

            resp.EnsureSuccessStatusCode();

            totalPages = long.Parse(resp.Headers.GetValues("X-Pages").First());

            var orders = await resp.Content.ReadFromJsonAsync<List<MarketOrder>>()
                         ?? new List<MarketOrder>();

            foreach (var o in orders)
            {
                o.RegionId = RegionId;
            }

            var newOrders = orders
                .Where(o => seenOrderIds.Add(o.OrderId))
                .ToList();

            _db.MarketOrders.AddRange(newOrders);
            await _db.SaveChangesAsync();

            totalInserted += newOrders.Count;


            //cleares tracked entities from EF memory, helps with big imports
            _db.ChangeTracker.Clear();

            //print progress
            Console.WriteLine($"Page {page}/{totalPages}: inserted {newOrders.Count:n0} (raw {orders.Count:n0}, total {totalInserted:n0})");
        }

        //stops stopwatch
        sw.Stop();

        //final summary
        Console.WriteLine($"DONE. Inserted {totalInserted:n0} sell orders for region {RegionId} in {sw.Elapsed}.");
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

        return http;
    }
}