using System.Text.Json.Serialization;

namespace EveOnTrader.Worker.Models;

public class UniverseNameResult
{
    [JsonPropertyName("category")]
    public string Category { get; set; } = "";

    [JsonPropertyName("id")]
    public long Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}