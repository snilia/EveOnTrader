using System.Text.Json.Serialization;

namespace EveOnTrader.Worker.Models;

// UniverseTypeResult stores ESI universe type details needed for local item metadata.
public class UniverseTypeResult
{
    [JsonPropertyName("type_id")]
    public long TypeId { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("volume")]
    public decimal VolumeM3 { get; set; }

    [JsonPropertyName("packaged_volume")]
    public decimal PackagedVolumeM3 { get; set; }
}