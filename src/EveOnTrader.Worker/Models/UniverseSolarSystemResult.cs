using System.Text.Json.Serialization;

namespace EveOnTrader.Worker.Models;

public class UniverseSolarSystemResult
{
    [JsonPropertyName("constellation_id")]
    public long ConstellationId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("security_status")]
    public double SecurityStatus { get; set; }
}
