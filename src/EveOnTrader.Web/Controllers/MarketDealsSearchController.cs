using EveOnTrader.Core.DealFinding.Models;
using EveOnTrader.Core.DealFinding.Services;
using EveOnTrader.Core.RouteFinding;
using EveOnTrader.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace EveOnTrader.Web.Controllers;

// MarketDealsSearchController shows generic market-deal search pages.
public class MarketDealsSearchController : Controller
{
    private readonly MarketDealFinder _marketDealFinder;

    // Creates controller with market deal finder.
    public MarketDealsSearchController(MarketDealFinder marketDealFinder)
    {
        _marketDealFinder = marketDealFinder;
    }

    // GET: /MarketDealsSearch
    // Loads all chosen sell stations and buy stations, runs market deal search, and shows best step per item.
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
            MinTotalProfit = model.Input.MinTotalProfit,
            MinTotalRoi = model.Input.MinTotalRoi,
            MinProfitPerJump = model.Input.MinProfitPerJump,
            MaxSteps = model.Input.MaxSteps,
            BrokerFeeRate = model.Input.BrokerFeeRate,
            SalesTaxRate = model.Input.SalesTaxRate,
            RouteSecurityLimit = model.Input.RouteSecurityLimit
        };

        var routeResults = await _marketDealFinder.FindDealsAsync(
            sellStationIds,
            buyStationIds,
            ToRouteSecurityPreference(model.Input.RouteSecurityLimit),
            options);

        model.Result.DealRows = routeResults
            .SelectMany(MapRouteResultRows)
            .OrderByDescending(x => x.TotalProfit)
            .ThenBy(x => x.SellStationName)
            .ThenBy(x => x.BuyStationName)
            .ThenBy(x => x.TypeId)
            .ToList();

        return View(model);
    }

    // MapRouteResultRows maps route deal result items into display rows.
    private static IEnumerable<MarketDealsSearchRowViewModel> MapRouteResultRows(RouteDealResult routeResult)
    {
        return routeResult.Items
            .Select(item => new
            {
                Item = item,
                BestStep = item.Steps.Last()
            })
            .Select(x => new MarketDealsSearchRowViewModel
            {
                SellStationId = routeResult.SourceLocationId ?? 0,
                SellStationName = GetUsefulName(routeResult.SourceLocationName, routeResult.SourceLocationId),
                SellRegionId = routeResult.SourceRegionId,
                SellRegionName = routeResult.SourceRegionName,
                BuyStationId = routeResult.DestinationLocationId,
                BuyStationName = GetUsefulName(routeResult.DestinationLocationName, routeResult.DestinationLocationId),
                BuyRegionId = routeResult.DestinationRegionId,
                BuyRegionName = routeResult.DestinationRegionName,
                JumpCount = routeResult.JumpCount,
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
            });
    }

    // NormalizeModel fixes invalid or empty search defaults before running search.
    private static void NormalizeModel(MarketDealsSearchInputViewModel input)
    {
        input.SellStationIdsText ??= "";
        input.BuyStationIdsText ??= "";

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

    // ToRouteSecurityPreference maps deal-finder security option into route-distance security option.
    private static RouteSecurityPreference ToRouteSecurityPreference(RouteSecurityLimit routeSecurityLimit)
    {
        return routeSecurityLimit switch
        {
            RouteSecurityLimit.HighSecOnly => RouteSecurityPreference.Secure,
            RouteSecurityLimit.LowSecAllowed => RouteSecurityPreference.Shortest,
            RouteSecurityLimit.NullSecAllowed => RouteSecurityPreference.Shortest,
            _ => RouteSecurityPreference.Shortest
        };
    }

    // GetUsefulName returns stored name if useful, otherwise falls back to ID text.
    private static string GetUsefulName(string name, long? id)
    {
        if (!string.IsNullOrWhiteSpace(name) &&
            !name.StartsWith("(unknown", StringComparison.OrdinalIgnoreCase))
        {
            return name;
        }

        return id?.ToString("N0") ?? "";
    }
}