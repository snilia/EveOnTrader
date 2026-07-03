using System.Text.Json.Serialization;

namespace EveOnTrader.Infra.Esi.Models;

// EsiMarketRow stores one raw market order row returned by ESI.
public class EsiMarketRow
{
    [JsonPropertyName("order_id")]
    public long OrderId { get; set; }

    [JsonPropertyName("is_buy_order")]
    public bool IsBuyOrder { get; set; }

    [JsonPropertyName("issued")]
    public DateTime Issued { get; set; }

    [JsonPropertyName("location_id")]
    public long LocationId { get; set; }

    [JsonPropertyName("system_id")]
    public long SystemId { get; set; }

    [JsonPropertyName("type_id")]
    public long TypeId { get; set; }

    [JsonPropertyName("price")]
    public decimal Price { get; set; }

    [JsonPropertyName("volume_remain")]
    public long VolumeRemain { get; set; }

    [JsonPropertyName("volume_total")]
    public long VolumeTotal { get; set; }

    [JsonPropertyName("min_volume")]
    public long MinVolume { get; set; }

    [JsonPropertyName("duration")]
    public long Duration { get; set; }

    [JsonPropertyName("range")]
    public string Range { get; set; } = "";
}