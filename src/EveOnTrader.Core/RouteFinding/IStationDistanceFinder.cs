namespace EveOnTrader.Core.RouteFinding;

// IStationDistanceFinder returns route jump counts between market locations.
public interface IStationDistanceFinder
{
    // GetJumpCountAsync returns jump count for one source/destination location pair.
    Task<int?> GetJumpCountAsync(
        long sourceLocationId,
        long destinationLocationId,
        RouteSecurityPreference securityPreference);

    // GetJumpCountsAsync returns jump counts for every source x destination location pair.
    Task<Dictionary<(long SourceLocationId, long DestinationLocationId), int?>> GetJumpCountsAsync(
        List<long> sourceLocationIds,
        List<long> destinationLocationIds,
        RouteSecurityPreference securityPreference);
}