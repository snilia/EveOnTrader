using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EveOnTrader.Core.Models;
using EveOnTrader.Infra;
using EveOnTrader.Infra.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

const int regionId = 10000002;                 // The Forge (Jita region). Change later if you want.
const string orderType = "sell";               // we start with sell only
const string compatibilityDate = "2025-12-16"; // from the ESI docs you saw

// Put the DB in a stable place so Web and Worker use the same file.
var dataDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "EveOnTrader");
Directory.CreateDirectory(dataDir);
var dbPath = Path.Combine(dataDir, "eve.db");
var connStr = $"Data Source={dbPath}";

var builder = Host.CreateApplicationBuilder(args);

// Avoid spamming SQL for every INSERT:
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);

// Register Infra (DbContext configured for SQLite)
builder.Services.AddInfra(connStr);

using var host = builder.Build();  // The using thing
using var scope = host.Services.CreateScope();

var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

Console.WriteLine($"DB Path: {dbPath}");
Console.WriteLine("Ensuring DB exists...");
await db.Database.EnsureCreatedAsync(); //creates db file + tables if nonexistant

Console.WriteLine("Clearing MarketOrders (fresh snapshot)...");
await db.Database.ExecuteSqlRawAsync("DELETE FROM MarketOrders"); //deletes all rows from table

// Speed improvement for bulk inserts:
db.ChangeTracker.AutoDetectChangesEnabled = false; //dont need change tracking for this bulk insert

using var http = new HttpClient { BaseAddress = new Uri("https://esi.evetech.net/latest/") };
http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

// Compatibility date: optional in general, but good practice and matches what you saw in Swagger.
// If this ever fails, you can update the date or switch to the `compatibility_date` query parameter.
http.DefaultRequestHeaders.TryAddWithoutValidation("X-Compatibility-Date", compatibilityDate);

var sw = Stopwatch.StartNew();

long totalInserted = 0;
long totalPages = 1;

for (var page = 1; page <= totalPages; page++)
{
    var url = $"markets/{regionId}/orders/?order_type={orderType}&datasource=tranquility&page={page}";
    using var resp = await http.GetAsync(url);  //the using here makes sure the response is disposed every iteration

    resp.EnsureSuccessStatusCode();

    // X-Pages tells us how many pages exist (only present after a successful response)
    totalPages = long.Parse(resp.Headers.GetValues("X-Pages").First());

    var orders = await resp.Content.ReadFromJsonAsync<List<MarketOrder>>() ?? new List<MarketOrder>();

    // RegionId is not in the response body, so we set it ourselves
    foreach (var o in orders)
        o.RegionId = regionId;

    // Insert this page
    db.MarketOrders.AddRange(orders);
    await db.SaveChangesAsync();

    totalInserted += orders.Count;

    // Clear tracked entities so memory doesn't grow forever
    db.ChangeTracker.Clear();

    Console.WriteLine($"Page {page}/{totalPages}: inserted {orders.Count:n0} (total {totalInserted:n0})");
}

sw.Stop();
Console.WriteLine($"DONE. Inserted {totalInserted:n0} sell orders for region {regionId} in {sw.Elapsed}.");