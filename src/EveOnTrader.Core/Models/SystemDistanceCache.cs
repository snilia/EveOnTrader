using EveOnTrader.Core.RouteFinding;

namespace EveOnTrader.Core.Models;

// SystemDistanceCache stores resolved jump count between two solar systems for one route security preference.
public class SystemDistanceCache
{
    public long SourceSolarSystemId { get; set; }
    public long DestinationSolarSystemId { get; set; }
    public RouteSecurityPreference SecurityPreference { get; set; }

    public int? JumpCount { get; set; }
    public DateTime ResolvedAtUtc { get; set; }
}