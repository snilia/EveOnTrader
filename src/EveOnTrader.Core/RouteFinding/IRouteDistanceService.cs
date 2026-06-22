namespace EveOnTrader.Core.RouteFinding;

// RouteSecurityPreference controls which route type should be used when calculating distance.
public enum RouteSecurityPreference
{
    Shortest = 0,
    Secure = 1,
    Insecure = 2
}

// IRouteDistanceService returns route distance between two market locations.
public interface IRouteDistanceService
{
    // GetJumpCountAsync returns jump count between two locations for chosen security preference, or null if route cannot be resolved.
    Task<int?> GetJumpCountAsync(
        long sourceLocationId,
        long destinationLocationId,
        RouteSecurityPreference securityPreference);
}