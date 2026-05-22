using EveOnTrader.Infra.Queries;
using Microsoft.AspNetCore.Mvc;

namespace EveOnTrader.Web.Controllers;

// RoutesController shows simple route-order pages for manual inspection.
public class RoutesController : Controller
{
    private readonly OrderInRouteQueryService _orderInRouteQueryService;

    // Creates controller with route-order query service.
    public RoutesController(OrderInRouteQueryService orderInRouteQueryService)
    {
        _orderInRouteQueryService = orderInRouteQueryService;
    }

    // GET: /Routes/SinqToJita
    // Loads grouped Sinq sells and Jita buy orders for first route-inspection page.
    public async Task<IActionResult> SinqToJita()
    {
        const long sourceRegionId = 10000032;
        const long destinationLocationId = 60003760;

        var route = await _orderInRouteQueryService.GetAllItemTypesOrderRouteAsync(
            sourceRegionId,
            destinationLocationId);

        ViewBag.SourceRegionName = route.Items
            .SelectMany(x => x.SourceSellOrders)
            .Select(x => x.RegionName)
            .FirstOrDefault() ?? sourceRegionId.ToString();

        ViewBag.DestinationLocationName = route.Items
            .SelectMany(x => x.DestinationBuyOrders)
            .Select(x => x.LocationName)
            .FirstOrDefault() ?? destinationLocationId.ToString();

        return View(route);
    }
}
