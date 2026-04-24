namespace EveOnTrader.Core.Models;

public class MarketLocation
{
    public long LocationId { get; set; }
    public int SolarSystemId { get; set; }
    public string Name { get; set; } = "";
    public KindValue Kind { get; set; }
    public bool? HasPublicDocking { get; set; }

    public enum KindValue
    {
        Unknown = 0,
        Station = 1,
        UpwellStructure = 2
    }
}
