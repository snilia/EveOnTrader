using System.Text.Json.Serialization;

namespace EveOnTrader.Infra.Esi.Models;

// EsiConstellation stores constellation details returned by ESI.
public class EsiConstellation
{
    [JsonPropertyName("region_id")]
    public long RegionId { get; set; }
}