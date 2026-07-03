using System.Text.Json.Serialization;

namespace EveOnTrader.Infra.Esi.Models;

// EsiMarketLocation stores station/location details returned by ESI.
public class EsiMarketLocation
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("system_id")]
    public long SystemId { get; set; }
}