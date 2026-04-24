namespace EveOnTrader.Core.Models;

public class SolarSystem
{
    public int SolarSystemId { get; set; }
    public int RegionId { get; set; }
    public string Name { get; set; } = "";
    public double SecurityStatus { get; set; }
}
