using System.Text.Json.Serialization;

namespace EveOnTrader.Worker.Models;

public class UniverseConstellationResult
{
    [JsonPropertyName("region_id")]
    public long RegionId { get; set; }
}
