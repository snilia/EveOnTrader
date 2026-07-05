namespace EveOnTrader.Core.RouteFinding;

// RouteSecurityPreference controls which route type should be used when calculating distance.
public enum RouteSecurityPreference
{
    Shortest = 0,
    Secure = 1,
    Insecure = 2
}