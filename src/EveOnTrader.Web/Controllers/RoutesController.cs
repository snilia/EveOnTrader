using EveOnTrader.Core.DealFinding.Models;
using EveOnTrader.Core.DealFinding.Services;
using EveOnTrader.Infra.Queries;
using Microsoft.AspNetCore.Mvc;

namespace EveOnTrader.Web.Controllers;

// RoutesController shows simple route-order pages for manual inspection.
public class RoutesController : Controller
{
    private readonly OrderInRouteQueryService _orderInRouteQueryService;
    private readonly RouteDealFinder _routeDealFinder;

    // Creates controller with route-order query service and route deal finder.
    public RoutesController(
        OrderInRouteQueryService orderInRouteQueryService,
        RouteDealFinder routeDealFinder)
    {
        _orderInRouteQueryService = orderInRouteQueryService;
        _routeDealFinder = routeDealFinder;
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

    // GET: /Routes/DodixieToJita
    // Loads grouped Dodixie-hub sells and Jita-hub buy orders for hub-to-hub inspection.
    public async Task<IActionResult> DodixieToJita()
    {
        const long sourceLocationId = 60011866;
        const long destinationLocationId = 60003760;

        var route = await _orderInRouteQueryService.GetAllItemTypesOrderRouteByLocationAsync(
            sourceLocationId,
            destinationLocationId);

        ViewBag.SourceLocationName = route.Items
            .SelectMany(x => x.SourceSellOrders)
            .Select(x => x.LocationName)
            .FirstOrDefault() ?? sourceLocationId.ToString();

        ViewBag.DestinationLocationName = route.Items
            .SelectMany(x => x.DestinationBuyOrders)
            .Select(x => x.LocationName)
            .FirstOrDefault() ?? destinationLocationId.ToString();

        return View(route);
    }

    // GET: /Routes/DodixieToJitaDeals
    // Loads Dodixie-hub to Jita-hub route, runs deal finder, and shows best cumulative step per item.
    public async Task<IActionResult> DodixieToJitaDeals(
        decimal? maxTotalBuyCost = null,
        decimal? maxTotalVolumeM3 = null,
        decimal? minTotalRoi = null,
        decimal? salesTaxRate = null)
    {
        const long sourceLocationId = 60008494;
        const long destinationLocationId = 60003760;

        var route = await _orderInRouteQueryService.GetAllItemTypesOrderRouteByLocationAsync(
            sourceLocationId,
            destinationLocationId);

        var options = new DealFinderOptions
        {
            MaxTotalBuyCost = maxTotalBuyCost,
            MaxTotalVolumeM3 = maxTotalVolumeM3,
            MinTotalRoi = minTotalRoi,
            SalesTaxRate = salesTaxRate ?? 0m,
            JumpCount = 1
        };

        var deals = _routeDealFinder.FindRouteDeals(route, options);

        ViewBag.SourceLocationName = route.Items
            .SelectMany(x => x.SourceSellOrders)
            .Select(x => x.LocationName)
            .FirstOrDefault() ?? sourceLocationId.ToString();

        ViewBag.DestinationLocationName = route.Items
            .SelectMany(x => x.DestinationBuyOrders)
            .Select(x => x.LocationName)
            .FirstOrDefault() ?? destinationLocationId.ToString();

        ViewBag.MaxTotalBuyCost = options.MaxTotalBuyCost;
        ViewBag.MaxTotalVolumeM3 = options.MaxTotalVolumeM3;
        ViewBag.MinTotalRoi = options.MinTotalRoi;
        ViewBag.SalesTaxRate = options.SalesTaxRate;

        return View(deals);
    }
}
