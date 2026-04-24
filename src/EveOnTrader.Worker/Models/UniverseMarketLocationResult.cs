using System.Text.Json.Serialization;

namespace EveOnTrader.Worker.Models;

public class UniverseMarketLocationResult
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("system_id")]
    public long SystemId { get; set; }
}
