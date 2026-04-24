namespace EveOnTrader.Core.Models;

public class SolarSystem
{
    public long SolarSystemId { get; set; }
    public long RegionId { get; set; }
    public string Name { get; set; } = "";
    public double SecurityStatus { get; set; }
}
