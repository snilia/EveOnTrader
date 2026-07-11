using EveOnTrader.Core.MarketImport;
using EveOnTrader.Infra;
using EveOnTrader.Infra.Data;
using EveOnTrader.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration
    .GetConnectionString("EveDatabase")
    ?? throw new InvalidOperationException(
        "Connection string 'EveDatabase' is not configured.");

// Show import progress from Infra, but hide noisy EF/HTTP internals.
builder.Logging.ClearProviders();

builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "HH:mm:ss ";
});

builder.Logging.SetMinimumLevel(LogLevel.Information);
builder.Logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
builder.Logging.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
builder.Logging.AddFilter("EveOnTrader.Infra", LogLevel.Information);

// Register Infra.
builder.Services.AddInfra(connectionString);

using var host = builder.Build();
using var scope = host.Services.CreateScope();

var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
await db.Database.EnsureCreatedAsync();

var regionCatalogQuery = scope.ServiceProvider.GetRequiredService<IRegionCatalogQuery>();
var marketOrderImportService = scope.ServiceProvider.GetRequiredService<IMarketOrderImportService>();

Console.WriteLine("Loading regions...");

var regions = await regionCatalogQuery.GetRegionsAsync();

if (regions.Count == 0)
{
    Console.WriteLine("No regions available.");
    return;
}

var importRequest = RegionImportPrompt.Prompt(regions);

if (importRequest.Slices.Count == 0)
{
    Console.WriteLine("No import slices selected.");
    return;
}

var result = await marketOrderImportService.ImportAsync(importRequest);

Console.WriteLine();
Console.WriteLine("DONE.");
Console.WriteLine($"Selection: {result.SelectionName}");
Console.WriteLine($"Batch ID: {result.ImportBatchId}");
Console.WriteLine($"Imported at UTC: {result.ImportedAtUtc:O}");
Console.WriteLine($"Request slices selected: {result.RequestCount:n0}");
Console.WriteLine($"Normalized request slices: {result.NormalizedRequestCount:n0}");
Console.WriteLine($"Inserted {result.RegionsInserted:n0} new regions.");
Console.WriteLine($"Inserted {result.SolarSystemsInserted:n0} new solar systems.");
Console.WriteLine($"Deleted {result.DeletedMarketOrderCount:n0} old market orders.");
Console.WriteLine($"Inserted {result.InsertedMarketOrderCount:n0} market orders.");
Console.WriteLine($"Inserted {result.MarketLocationsInserted:n0} new market locations.");
Console.WriteLine($"Inserted {result.ItemTypeRefsInserted:n0} new item type refs.");
Console.WriteLine($"Total elapsed: {result.Elapsed}.");