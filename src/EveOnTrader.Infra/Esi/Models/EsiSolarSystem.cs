using System.Text.Json.Serialization;

namespace EveOnTrader.Infra.Esi.Models;

// EsiSolarSystem stores solar-system details returned by ESI.
public class EsiSolarSystem
{
    [JsonPropertyName("constellation_id")]
    public long ConstellationId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("security_status")]
    public double SecurityStatus { get; set; }
}