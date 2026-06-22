using EveOnTrader.Core.RouteFinding;

namespace EveOnTrader.Infra.RouteFinding;

// RouteDistanceService returns route distance between two market locations.
public class RouteDistanceService : IRouteDistanceService
{
    // GetJumpCountAsync temporarily returns 1 until real ESI route calculation exists.
    public Task<int?> GetJumpCountAsync(
        long sourceLocationId,
        long destinationLocationId,
        RouteSecurityPreference securityPreference)
    {
        return Task.FromResult<int?>(1);
    }
}