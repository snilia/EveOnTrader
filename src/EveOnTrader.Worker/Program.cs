using System.Net.Http.Json;
using EveOnTrader.Infra;
using EveOnTrader.Infra.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;


Console.WriteLine("Calling EVE ESI...");

using var http = new HttpClient
{
    BaseAddress = new Uri("https://esi.evetech.net/latest/")
};

// Simple public endpoint that requires no auth:
var status = await http.GetFromJsonAsync<EsiStatus>("status/?datasource=tranquility");

if (status is null)
{
    Console.WriteLine("ESI returned no data (null).");
    return;
}

Console.WriteLine($"Players online: {status.players}");
Console.WriteLine($"Server version: {status.server_version}");
Console.WriteLine($"Start time:     {status.start_time}");




var builder = Host.CreateApplicationBuilder(args);

// register DbContext (SQLite)
builder.Services.AddInfra("Data Source=eve.db");

using var host = builder.Build();
using var scope = host.Services.CreateScope();

var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

// B6: create DB + tables if missing
await db.Database.EnsureCreatedAsync();

Console.WriteLine("SQLite DB ensured (created if it didn't exist).");

public sealed class EsiStatus
{
    public int players { get; set; }
    public string server_version { get; set; } = "";
    public DateTime start_time { get; set; }

}



