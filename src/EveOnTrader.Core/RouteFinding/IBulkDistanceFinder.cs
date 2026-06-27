namespace EveOnTrader.Core.RouteFinding;

// IBulkDistanceFinder returns route distances for many source/destination location combinations.
public interface IBulkDistanceFinder
{
    // GetJumpCountsAsync returns jump counts for every source x destination location combination.
    Task<Dictionary<(long SourceLocationId, long DestinationLocationId), int?>> GetJumpCountsAsync(
        List<long> sourceLocationIds,
        List<long> destinationLocationIds,
        RouteSecurityPreference securityPreference);
}