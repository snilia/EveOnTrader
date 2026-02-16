using System.Net.Http.Json;

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

public sealed class EsiStatus
{
    public int players { get; set; }
    public string server_version { get; set; } = "";
    public DateTime start_time { get; set; }
}
