using EveOnTrader.Core.RouteFinding;

namespace EveOnTrader.Infra.RouteFinding;

// BulkDistanceFinder returns route distances for many market location combinations.
public class BulkDistanceFinder : IBulkDistanceFinder
{
    // GetJumpCountsAsync temporarily returns 1 for every source/destination pair until real route calculation exists.
    public Task<Dictionary<(long SourceLocationId, long DestinationLocationId), int?>> GetJumpCountsAsync(
        List<long> sourceLocationIds,
        List<long> destinationLocationIds,
        RouteSecurityPreference securityPreference)
    {
        ArgumentNullException.ThrowIfNull(sourceLocationIds);
        ArgumentNullException.ThrowIfNull(destinationLocationIds);

        var distinctSourceLocationIds = sourceLocationIds
            .Where(x => x > 0)
            .Distinct()
            .ToList();

        var distinctDestinationLocationIds = destinationLocationIds
            .Where(x => x > 0)
            .Distinct()
            .ToList();

        var jumpCounts = new Dictionary<(long SourceLocationId, long DestinationLocationId), int?>();

        foreach (var sourceLocationId in distinctSourceLocationIds)
        {
            foreach (var destinationLocationId in distinctDestinationLocationIds)
            {
                jumpCounts[(sourceLocationId, destinationLocationId)] = 1;
            }
        }

        return Task.FromResult(jumpCounts);
    }
}