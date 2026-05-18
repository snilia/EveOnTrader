using System.Net.Http.Headers;
using EveOnTrader.Infra;
using EveOnTrader.Worker;
using EveOnTrader.Worker.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

const string dbFolderName = "EveOnTrader";

var dataDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    dbFolderName);
Directory.CreateDirectory(dataDir);

var dbPath = Path.Combine(dataDir, "eve.db");
var connStr = $"Data Source={dbPath}";

var builder = Host.CreateApplicationBuilder(args);

// Avoid spamming SQL for every INSERT:
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);


// Register Infra (DbContext configured for SQLite)
builder.Services.AddInfra(connStr);

// Register Worker services
builder.Services.AddHttpClient<EsiClient>(client =>
{
    client.BaseAddress = new Uri("https://esi.evetech.net/latest/");

    client.DefaultRequestHeaders.Accept.Add(
        new MediaTypeWithQualityHeaderValue("application/json"));

    client.DefaultRequestHeaders.TryAddWithoutValidation(
        "X-Compatibility-Date",
        "2025-12-16");

    client.DefaultRequestHeaders.UserAgent.ParseAdd("EveOnTrader.Worker/1.0");
});

builder.Services.AddTransient<ItemTypeNameSyncService>();
builder.Services.AddTransient<MarketImportRunner>();
builder.Services.AddTransient<UniverseSyncService>();
builder.Services.AddTransient<WorkerLookupService>();

using var host = builder.Build();
using var scope = host.Services.CreateScope();

var workerLookupService = scope.ServiceProvider.GetRequiredService<WorkerLookupService>();
var runner = scope.ServiceProvider.GetRequiredService<MarketImportRunner>();

var regions = await workerLookupService.GetAvailableRegionsAsync();

if (regions.Count == 0)
{
    Console.WriteLine("No regions available.");
    return;
}

var importOptions = RegionImportPrompt.Prompt(regions);

await runner.RunAsync(dbPath, importOptions);
