using EveOnTrader.Core.MarketImport;
using EveOnTrader.Core.Models;
using EveOnTrader.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace EveOnTrader.Web.Controllers;

// MarketOrderUpdateController lets Web refresh market orders from ESI.
public class MarketOrderUpdateController : Controller
{
    private readonly IRegionCatalogQuery _regionCatalogQuery;
    private readonly IMarketOrderImportService _marketOrderImportService;

    // Creates controller with region catalog and market order import service.
    public MarketOrderUpdateController(
        IRegionCatalogQuery regionCatalogQuery,
        IMarketOrderImportService marketOrderImportService)
    {
        _regionCatalogQuery = regionCatalogQuery;
        _marketOrderImportService = marketOrderImportService;
    }

    // GET: /MarketOrderUpdate
    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var model = new MarketOrderUpdateViewModel();
        await LoadRegionsAsync(model);

        return View(model);
    }

    // POST: /MarketOrderUpdate
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(MarketOrderUpdateViewModel model)
    {
        NormalizeModel(model);
        await LoadRegionsAsync(model);

        var sourceRegionIds = ParseRegionInput(
            model.SourceRegionIdsText,
            model.AvailableRegions,
            out var sourceErrors);

        var destinationRegionIds = ParseRegionInput(
            model.DestinationRegionIdsText,
            model.AvailableRegions,
            out var destinationErrors);

        var errors = new List<string>();
        errors.AddRange(sourceErrors.Select(x => $"Source: {x}"));
        errors.AddRange(destinationErrors.Select(x => $"Destination: {x}"));

        if (sourceRegionIds.Count == 0 && destinationRegionIds.Count == 0)
        {
            errors.Add("Enter at least one source or destination region.");
        }

        if (errors.Count > 0)
        {
            model.ErrorMessage = string.Join(" ", errors);
            return View(model);
        }

        var slices = new List<MarketOrderImportSlice>();

        slices.AddRange(
            sourceRegionIds.Select(regionId => new MarketOrderImportSlice
            {
                RegionId = regionId,
                Side = MarketOrderSide.Sell,
                TypeId = null
            }));

        slices.AddRange(
            destinationRegionIds.Select(regionId => new MarketOrderImportSlice
            {
                RegionId = regionId,
                Side = MarketOrderSide.Buy,
                TypeId = null
            }));

        var request = new MarketOrderImportRequest
        {
            SelectionName = BuildSelectionName(sourceRegionIds, destinationRegionIds),
            Slices = slices
        };

        try
        {
            model.Result = await _marketOrderImportService.ImportAsync(request);
        }
        catch (Exception ex)
        {
            model.ErrorMessage = ex.Message;
        }

        return View(model);
    }

    // LoadRegionsAsync loads available regions for validation and autocomplete.
    private async Task LoadRegionsAsync(MarketOrderUpdateViewModel model)
    {
        model.AvailableRegions = await _regionCatalogQuery.GetRegionsAsync();
    }

    // NormalizeModel fixes null strings before parsing.
    private static void NormalizeModel(MarketOrderUpdateViewModel model)
    {
        model.SourceRegionIdsText ??= "";
        model.DestinationRegionIdsText ??= "";
    }

    // ParseRegionInput parses IDs, region names, and "all" into region IDs.
    private static List<long> ParseRegionInput(
        string value,
        List<Region> availableRegions,
        out List<string> errors)
    {
        errors = [];

        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        var allRegionIds = availableRegions
            .Select(x => x.RegionId)
            .OrderBy(x => x)
            .ToList();

        var regionsById = availableRegions
            .ToDictionary(x => x.RegionId);

        var regionsByName = availableRegions
            .GroupBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        var tokens = value.Split(
            [',', ';', '\r', '\n'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var regionIds = new List<long>();

        foreach (var token in tokens)
        {
            if (string.Equals(token, "all", StringComparison.OrdinalIgnoreCase))
            {
                regionIds.AddRange(allRegionIds);
                continue;
            }

            if (long.TryParse(token, out var regionId))
            {
                if (regionsById.ContainsKey(regionId))
                {
                    regionIds.Add(regionId);
                }
                else
                {
                    errors.Add($"Unknown region ID: {regionId}.");
                }

                continue;
            }

            if (regionsByName.TryGetValue(token, out var region))
            {
                regionIds.Add(region.RegionId);
                continue;
            }

            errors.Add($"Unknown region name or ID: {token}.");
        }

        return regionIds
            .Distinct()
            .OrderBy(x => x)
            .ToList();
    }

    // BuildSelectionName builds short import selection label.
    private static string BuildSelectionName(List<long> sourceRegionIds, List<long> destinationRegionIds)
    {
        return $"Source sell regions: {sourceRegionIds.Count:n0} | Destination buy regions: {destinationRegionIds.Count:n0}";
    }
}