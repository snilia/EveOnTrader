using System.Text.Json.Serialization;

namespace EveOnTrader.Infra.Esi.Models;

// EsiUniverseName stores one name resolved by ESI universe/names.
public class EsiUniverseName
{
    [JsonPropertyName("category")]
    public string Category { get; set; } = "";

    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}