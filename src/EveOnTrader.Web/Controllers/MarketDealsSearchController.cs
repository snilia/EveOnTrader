using EveOnTrader.Core.DealFinding.Models;
using EveOnTrader.Core.DealFinding.Services;
using EveOnTrader.Core.ReadModels;
using EveOnTrader.Infra.Queries;
using EveOnTrader.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace EveOnTrader.Web.Controllers;

// MarketDealsSearchController shows generic market-deal search pages.
public class MarketDealsSearchController : Controller
{
    private readonly OrderInRouteQueryService _orderInRouteQueryService;
    private readonly RouteDealFinder _routeDealFinder;

    // Creates controller with route-order query service and route deal finder.
    public MarketDealsSearchController(
        OrderInRouteQueryService orderInRouteQueryService,
        RouteDealFinder routeDealFinder)
    {
        _orderInRouteQueryService = orderInRouteQueryService;
        _routeDealFinder = routeDealFinder;
    }

    // GET: /MarketDealsSearch
    // Loads all chosen sell stations and buy stations, runs station-pair deal searches, and shows best step per item.
    public async Task<IActionResult> Index([FromQuery] MarketDealsSearchViewModel model)
    {
        NormalizeModel(model.Input);

        var sellStationIds = ParseStationIds(model.Input.SellStationIdsText, out var invalidSellStationIds);
        var buyStationIds = ParseStationIds(model.Input.BuyStationIdsText, out var invalidBuyStationIds);

        model.Result.HasSearched = HasAnyStationInput(model.Input);
        model.Result.SellStationCount = sellStationIds.Count;
        model.Result.BuyStationCount = buyStationIds.Count;
        model.Result.RoutePairCount = sellStationIds.Count * buyStationIds.Count;

        if (invalidSellStationIds.Count > 0 || invalidBuyStationIds.Count > 0)
        {
            model.Result.SearchMessage = BuildInvalidStationMessage(invalidSellStationIds, invalidBuyStationIds);
        }

        if (!model.Result.HasSearched)
        {
            return View(model);
        }

        if (sellStationIds.Count == 0 || buyStationIds.Count == 0)
        {
            model.Result.SearchMessage = AppendMessage(
                model.Result.SearchMessage,
                "Enter at least one sell station and one buy station.");

            return View(model);
        }

        var options = new DealFinderOptions
        {
            MaxTotalBuyCost = model.Input.MaxTotalBuyCost,
            MaxTotalVolumeM3 = model.Input.MaxTotalVolumeM3,
            MinTotalRoi = model.Input.MinTotalRoi,
            MaxSteps = model.Input.MaxSteps,
            JumpCount = model.Input.JumpCount,
            BrokerFeeRate = model.Input.BrokerFeeRate,
            SalesTaxRate = model.Input.SalesTaxRate,
            RouteSecurityLimit = model.Input.RouteSecurityLimit
        };

        var rows = new List<MarketDealsSearchRowViewModel>();

        foreach (var sellStationId in sellStationIds)
        {
            foreach (var buyStationId in buyStationIds)
            {
                var pairRows = await BuildDealRowsForStationPairAsync(
                    sellStationId,
                    buyStationId,
                    options);

                rows.AddRange(pairRows);
            }
        }

        model.Result.DealRows = rows
            .Where(x => !model.Input.MinTotalProfit.HasValue || x.TotalProfit >= model.Input.MinTotalProfit.Value)
            .Where(x => !model.Input.MinProfitPerJump.HasValue || x.TotalProfitPerJump >= model.Input.MinProfitPerJump.Value)
            .OrderByDescending(x => x.TotalProfit)
            .ThenBy(x => x.SellStationName)
            .ThenBy(x => x.BuyStationName)
            .ThenBy(x => x.TypeId)
            .ToList();

        return View(model);
    }

    // BuildDealRowsForStationPairAsync reuses existing station-to-station route query and deal finder for one station pair.
    private async Task<List<MarketDealsSearchRowViewModel>> BuildDealRowsForStationPairAsync(
        long sellStationId,
        long buyStationId,
        DealFinderOptions options)
    {
        var route = await _orderInRouteQueryService.GetAllItemTypesOrderRouteByLocationAsync(
            sellStationId,
            buyStationId);

        var sellStationName = ResolveSellStationName(route, sellStationId);
        var buyStationName = ResolveBuyStationName(route, buyStationId);
        var sellRegionId = ResolveSellRegionId(route);
        var sellRegionName = ResolveSellRegionName(route);
        var buyRegionId = ResolveBuyRegionId(route);
        var buyRegionName = ResolveBuyRegionName(route);

        var deals = _routeDealFinder.FindRouteDeals(route, options);

        return deals.Items
            .Select(item => new
            {
                Item = item,
                BestStep = item.Steps.Last()
            })
            .Select(x => new MarketDealsSearchRowViewModel
            {
                SellStationId = sellStationId,
                SellStationName = sellStationName,
                SellRegionId = sellRegionId,
                SellRegionName = sellRegionName,
                BuyStationId = buyStationId,
                BuyStationName = buyStationName,
                BuyRegionId = buyRegionId,
                BuyRegionName = buyRegionName,
                JumpCount = deals.JumpCount,
                TypeId = x.Item.TypeId,
                TypeName = x.Item.TypeName,
                StepCount = x.Item.Steps.Count,
                TotalUnits = x.BestStep.TotalUnits,
                TotalVolumeM3 = x.BestStep.TotalVolumeM3,
                TotalBuyCost = x.BestStep.TotalBuyCost,
                TotalSellRevenue = x.BestStep.TotalSellRevenue,
                TotalProfit = x.BestStep.TotalProfit,
                TotalRoi = x.BestStep.TotalRoi,
                TotalProfitPerVolumeM3 = x.BestStep.TotalProfitPerVolumeM3,
                TotalProfitPerJump = x.BestStep.TotalProfitPerJump,
                BestSourceSellPrice = x.Item.BestSourceSellPrice,
                BestDestinationBuyPrice = x.Item.BestDestinationBuyPrice
            })
            .ToList();
    }

    // NormalizeModel fixes invalid or empty search defaults before running search.
    private static void NormalizeModel(MarketDealsSearchInputViewModel input)
    {
        input.SellStationIdsText ??= "";
        input.BuyStationIdsText ??= "";

        if (input.JumpCount <= 0)
        {
            input.JumpCount = 1;
        }

        if (input.BrokerFeeRate < 0m)
        {
            input.BrokerFeeRate = 0m;
        }

        if (input.SalesTaxRate < 0m)
        {
            input.SalesTaxRate = 0m;
        }
    }

    // HasAnyStationInput returns true after user enters at least one station input.
    private static bool HasAnyStationInput(MarketDealsSearchInputViewModel input)
    {
        return !string.IsNullOrWhiteSpace(input.SellStationIdsText) ||
               !string.IsNullOrWhiteSpace(input.BuyStationIdsText);
    }

    // ParseStationIds parses comma, space, semicolon, or newline separated station IDs.
    private static List<long> ParseStationIds(string value, out List<string> invalidParts)
    {
        invalidParts = [];

        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var stationIds = new List<long>();
        var parts = value.Split(
            [',', ';', '\r', '\n', '\t', ' '],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            if (long.TryParse(part, out var stationId) && stationId > 0)
            {
                stationIds.Add(stationId);
            }
            else
            {
                invalidParts.Add(part);
            }
        }

        return stationIds
            .Distinct()
            .ToList();
    }

    // BuildInvalidStationMessage builds one user-facing message for invalid station ID tokens.
    private static string BuildInvalidStationMessage(
        List<string> invalidSellStationIds,
        List<string> invalidBuyStationIds)
    {
        var parts = new List<string>();

        if (invalidSellStationIds.Count > 0)
        {
            parts.Add($"Invalid sell station IDs: {string.Join(", ", invalidSellStationIds)}.");
        }

        if (invalidBuyStationIds.Count > 0)
        {
            parts.Add($"Invalid buy station IDs: {string.Join(", ", invalidBuyStationIds)}.");
        }

        return string.Join(" ", parts);
    }

    // AppendMessage combines validation/search messages.
    private static string AppendMessage(string currentMessage, string nextMessage)
    {
        if (string.IsNullOrWhiteSpace(currentMessage))
        {
            return nextMessage;
        }

        return $"{currentMessage} {nextMessage}";
    }

    // ResolveSellStationName gets source station name from route data or known-hub fallback.
    private static string ResolveSellStationName(AllItemTypesOrderRoute route, long sellStationId)
    {
        return route.Items
            .SelectMany(x => x.SourceSellOrders)
            .Select(x => x.LocationName)
            .FirstOrDefault(IsUsefulName)
            ?? GetKnownStationName(sellStationId);
    }

    // ResolveBuyStationName gets destination station name from route data or known-hub fallback.
    private static string ResolveBuyStationName(AllItemTypesOrderRoute route, long buyStationId)
    {
        return route.Items
            .SelectMany(x => x.DestinationBuyOrders)
            .Select(x => x.LocationName)
            .FirstOrDefault(IsUsefulName)
            ?? GetKnownStationName(buyStationId);
    }

    // GetKnownStationName maps main trade-hub station IDs to readable names.
    private static string GetKnownStationName(long locationId)
    {
        return locationId switch
        {
            60003760 => "Jita IV - Moon 4 - Caldari Navy Assembly Plant",
            60008494 => "Amarr VIII (Oris) - Emperor Family Academy",
            60011866 => "Dodixie IX - Moon 20 - Federation Navy Assembly Plant",
            60005686 => "Hek VIII - Moon 12 - Boundless Creation Factory",
            60004588 => "Rens VI - Moon 8 - Brutor Tribe Treasury",
            _ => locationId.ToString("N0")
        };
    }

    // ResolveSellRegionId gets source region ID from loaded sell orders.
    private static long? ResolveSellRegionId(AllItemTypesOrderRoute route)
    {
        return route.Items
            .SelectMany(x => x.SourceSellOrders)
            .Select(x => (long?)x.RegionId)
            .FirstOrDefault();
    }

    // ResolveSellRegionName gets source region name from loaded sell orders.
    private static string ResolveSellRegionName(AllItemTypesOrderRoute route)
    {
        return route.Items
            .SelectMany(x => x.SourceSellOrders)
            .Select(x => x.RegionName)
            .FirstOrDefault(IsUsefulName)
            ?? "";
    }

    // ResolveBuyRegionId gets destination region ID from loaded buy orders.
    private static long? ResolveBuyRegionId(AllItemTypesOrderRoute route)
    {
        return route.Items
            .SelectMany(x => x.DestinationBuyOrders)
            .Select(x => (long?)x.RegionId)
            .FirstOrDefault();
    }

    // ResolveBuyRegionName gets destination region name from loaded buy orders.
    private static string ResolveBuyRegionName(AllItemTypesOrderRoute route)
    {
        return route.Items
            .SelectMany(x => x.DestinationBuyOrders)
            .Select(x => x.RegionName)
            .FirstOrDefault(IsUsefulName)
            ?? "";
    }

    // IsUsefulName skips empty and placeholder names.
    private static bool IsUsefulName(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               !value.StartsWith("(unknown", StringComparison.OrdinalIgnoreCase);
    }
}