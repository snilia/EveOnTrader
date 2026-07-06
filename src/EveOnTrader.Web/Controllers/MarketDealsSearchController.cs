using EveOnTrader.Core.DealFinding.Models;
using EveOnTrader.Core.DealFinding.Services;
using EveOnTrader.Core.MarketImport;
using EveOnTrader.Core.RouteFinding;
using EveOnTrader.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace EveOnTrader.Web.Controllers;

// MarketDealsSearchController shows generic market-deal search pages.
public class MarketDealsSearchController : Controller
{
    private readonly MarketDealFinder _marketDealFinder;
    private readonly IMarketOrderImportService _marketOrderImportService;

    // Creates controller with market deal finder and market import service.
    public MarketDealsSearchController(
        MarketDealFinder marketDealFinder,
        IMarketOrderImportService marketOrderImportService)
    {
        _marketDealFinder = marketDealFinder;
        _marketOrderImportService = marketOrderImportService;
    }

    // GET: /MarketDealsSearch
    // Loads chosen sell stations/regions and buy stations, runs market deal search, and shows best step per item.
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] MarketDealsSearchViewModel model)
    {
        await RunSearchAsync(model);
        return View(model);
    }

    // POST: /MarketDealsSearch/RefreshAndSubmit
    // Refreshes source sell-region orders, then runs same search.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RefreshAndSubmit(MarketDealsSearchViewModel model)
    {
        NormalizeModel(model.Input);

        var sellRegionIds = ParseIds(model.Input.SellRegionIdsText, out var invalidSellRegionIds);

        if (invalidSellRegionIds.Count > 0)
        {
            model.RefreshMessage = $"Admin refresh skipped. Invalid sell region IDs: {string.Join(", ", invalidSellRegionIds)}.";
        }
        else if (sellRegionIds.Count == 0)
        {
            model.RefreshMessage = "Admin refresh skipped. Enter at least one sell region. Sell stations are not refreshed.";
        }
        else
        {
            var importRequest = new MarketOrderImportRequest
            {
                SelectionName = "Admin refresh source sell regions",
                Slices = sellRegionIds
                    .Select(regionId => new MarketOrderImportSlice
                    {
                        RegionId = regionId,
                        Side = MarketOrderSide.Sell,
                        TypeId = null
                    })
                    .ToList()
            };

            try
            {
                model.RefreshResult = await _marketOrderImportService.ImportAsync(importRequest);
                model.RefreshMessage = $"Admin refresh done. Inserted {model.RefreshResult.InsertedMarketOrderCount:n0} source sell orders.";
            }
            catch (Exception ex)
            {
                model.RefreshMessage = $"Admin refresh failed: {ex.Message}";
                return View("Index", model);
            }
        }

        await RunSearchAsync(model);
        return View("Index", model);
    }

    // RunSearchAsync validates inputs, runs deal search, and fills result rows.
    private async Task RunSearchAsync(MarketDealsSearchViewModel model)
    {
        NormalizeModel(model.Input);

        var sellStationIds = ParseIds(model.Input.SellStationIdsText, out var invalidSellStationIds);
        var sellRegionIds = ParseIds(model.Input.SellRegionIdsText, out var invalidSellRegionIds);
        var buyStationIds = ParseIds(model.Input.BuyStationIdsText, out var invalidBuyStationIds);

        model.Result.SellStationCount = sellStationIds.Count;
        model.Result.SellRegionCount = sellRegionIds.Count;
        model.Result.BuyStationCount = buyStationIds.Count;

        if (invalidSellStationIds.Count > 0 || invalidSellRegionIds.Count > 0 || invalidBuyStationIds.Count > 0)
        {
            model.ErrorMessage = BuildInvalidIdMessage(
                invalidSellStationIds,
                invalidSellRegionIds,
                invalidBuyStationIds);
        }

        if (!HasAnySearchInput(model.Input))
        {
            return;
        }

        if (sellStationIds.Count == 0 && sellRegionIds.Count == 0)
        {
            model.ErrorMessage = AppendMessage(
                model.ErrorMessage,
                "Enter at least one sell station or sell region.");

            return;
        }

        if (buyStationIds.Count == 0)
        {
            model.ErrorMessage = AppendMessage(
                model.ErrorMessage,
                "Enter at least one buy station.");

            return;
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
            sellRegionIds,
            buyStationIds,
            ToRouteSecurityPreference(model.Input.RouteSecurityLimit),
            options);

        model.Result.Rows = routeResults
            .SelectMany(MapRouteResultRows)
            .OrderByDescending(x => x.TotalProfit)
            .ThenBy(x => x.SellStationName)
            .ThenBy(x => x.BuyStationName)
            .ThenBy(x => x.TypeId)
            .ToList();
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
                SellStationId = routeResult.SourceLocationId,
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

                BestSellPrice = x.Item.BestSourceSellPrice,
                BestBuyPrice = x.Item.BestDestinationBuyPrice
            });
    }

    // NormalizeModel fixes invalid or empty search defaults before running search.
    private static void NormalizeModel(MarketDealsSearchInputViewModel input)
    {
        input.SellStationIdsText ??= "";
        input.SellRegionIdsText ??= "";
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

    // HasAnySearchInput returns true after user enters at least one search input.
    private static bool HasAnySearchInput(MarketDealsSearchInputViewModel input)
    {
        return !string.IsNullOrWhiteSpace(input.SellStationIdsText) ||
               !string.IsNullOrWhiteSpace(input.SellRegionIdsText) ||
               !string.IsNullOrWhiteSpace(input.BuyStationIdsText);
    }

    // ParseIds parses comma, space, semicolon, or newline separated IDs.
    private static List<long> ParseIds(string value, out List<string> invalidParts)
    {
        invalidParts = [];

        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var ids = new List<long>();
        var parts = value.Split(
            [',', ';', '\r', '\n', '\t', ' '],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            if (long.TryParse(part, out var id) && id > 0)
            {
                ids.Add(id);
            }
            else
            {
                invalidParts.Add(part);
            }
        }

        return ids
            .Distinct()
            .ToList();
    }

    // BuildInvalidIdMessage builds one user-facing message for invalid ID tokens.
    private static string BuildInvalidIdMessage(
        List<string> invalidSellStationIds,
        List<string> invalidSellRegionIds,
        List<string> invalidBuyStationIds)
    {
        var parts = new List<string>();

        if (invalidSellStationIds.Count > 0)
        {
            parts.Add($"Invalid sell station IDs: {string.Join(", ", invalidSellStationIds)}.");
        }

        if (invalidSellRegionIds.Count > 0)
        {
            parts.Add($"Invalid sell region IDs: {string.Join(", ", invalidSellRegionIds)}.");
        }

        if (invalidBuyStationIds.Count > 0)
        {
            parts.Add($"Invalid buy station IDs: {string.Join(", ", invalidBuyStationIds)}.");
        }

        return string.Join(" ", parts);
    }

    // AppendMessage combines validation/search messages.
    private static string AppendMessage(string? currentMessage, string nextMessage)
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