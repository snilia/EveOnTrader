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

// Register Infra (DbContext configured for SQLite)
builder.Services.AddInfra(connStr);

// Register Worker services
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
