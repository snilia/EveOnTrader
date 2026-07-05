using System.Net.Http.Json;
using EveOnTrader.Core.RouteFinding;

namespace EveOnTrader.Infra.Esi;

// EsiDistanceClient gets route systems needed to calculate/validate distance.
public class EsiDistanceClient
{
    private readonly EsiHttpClient _esiHttpClient;

    // Creates distance client with shared ESI HTTP wrapper.
    public EsiDistanceClient(EsiHttpClient esiHttpClient)
    {
        _esiHttpClient = esiHttpClient;
    }

    // GetRouteSolarSystemIdsAsync returns ESI route solar system IDs for selected route type.
    public async Task<List<long>?> GetRouteSolarSystemIdsAsync(
        long sourceSolarSystemId,
        long destinationSolarSystemId,
        RouteSecurityPreference securityPreference,
        CancellationToken cancellationToken = default)
    {
        if (sourceSolarSystemId <= 0 || destinationSolarSystemId <= 0)
        {
            return null;
        }

        if (sourceSolarSystemId == destinationSolarSystemId)
        {
            return [sourceSolarSystemId];
        }

        var flag = ToEsiRouteFlag(securityPreference);
        var url = $"route/{sourceSolarSystemId}/{destinationSolarSystemId}/?datasource=tranquility&flag={flag}";

        using var resp = await _esiHttpClient.TryGetAsync(
            url,
            $"route distance {sourceSolarSystemId} -> {destinationSolarSystemId} ({flag})",
            cancellationToken);

        if (!resp.IsSuccessStatusCode)
        {
            return null;
        }

        var routeSolarSystemIds = await resp.Content.ReadFromJsonAsync<List<long>>(cancellationToken)
                                  ?? [];

        return routeSolarSystemIds.Count == 0
            ? null
            : routeSolarSystemIds;
    }

    // ToEsiRouteFlag maps app route preference to ESI route flag.
    private static string ToEsiRouteFlag(RouteSecurityPreference securityPreference)
    {
        return securityPreference switch
        {
            RouteSecurityPreference.Secure => "secure",
            RouteSecurityPreference.Insecure => "insecure",
            _ => "shortest"
        };
    }
}