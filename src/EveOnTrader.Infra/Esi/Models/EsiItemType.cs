using System.Text.Json.Serialization;

namespace EveOnTrader.Infra.Esi.Models;

// EsiItemType stores item type details returned by ESI.
public class EsiItemType
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