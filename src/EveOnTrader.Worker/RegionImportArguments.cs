using EveOnTrader.Core.MarketImport;
using EveOnTrader.Core.Models;

namespace EveOnTrader.Worker;

// RegionImportArguments converts command-line region choices into market import request.
public static class RegionImportArguments
{
    private const string SellRegionsOption = "--sell-regions";
    private const string BuyRegionsOption = "--buy-regions";

    // IsHelpRequested returns true when command-line arguments request usage help.
    public static bool IsHelpRequested(string[] args)
    {
        return args.Any(arg =>
            arg.Equals("--help", StringComparison.OrdinalIgnoreCase) ||
            arg.Equals("-h", StringComparison.OrdinalIgnoreCase));
    }

    // TryBuildRequest validates command-line arguments and builds market import request.
    public static bool TryBuildRequest(
        string[] args,
        IReadOnlyList<Region> availableRegions,
        out MarketOrderImportRequest request,
        out string error)
    {
        request = new MarketOrderImportRequest();
        error = "";

        if (!TryReadOptionValues(args, out var sellValue, out var buyValue, out error))
        {
            return false;
        }

        var availableRegionIds = availableRegions
            .Select(region => region.RegionId)
            .ToHashSet();

        var allRegionIds = availableRegionIds
            .OrderBy(regionId => regionId)
            .ToList();

        if (!TryResolveRegionSelection(
                sellValue,
                SellRegionsOption,
                availableRegionIds,
                allRegionIds,
                out var sellSelectionName,
                out var sellRegionIds,
                out error))
        {
            return false;
        }

        if (!TryResolveRegionSelection(
                buyValue,
                BuyRegionsOption,
                availableRegionIds,
                allRegionIds,
                out var buySelectionName,
                out var buyRegionIds,
                out error))
        {
            return false;
        }

        var slices = BuildSlices(sellRegionIds, MarketOrderSide.Sell)
            .Concat(BuildSlices(buyRegionIds, MarketOrderSide.Buy))
            .ToList();

        if (slices.Count == 0)
        {
            error = "Sell and buy regions cannot both be 'none'.";
            return false;
        }

        request = new MarketOrderImportRequest
        {
            SelectionName = $"Sell: {sellSelectionName} | Buy: {buySelectionName}",
            Slices = slices
        };

        return true;
    }

    // PrintUsage writes supported command-line arguments and examples.
    public static void PrintUsage()
    {
        Console.WriteLine(
            """
            Usage:
              EveOnTrader.Worker --sell-regions <value> --buy-regions <value>

            Values:
              all                  All available regions
              none                 No regions for this order side
              id,id                Comma-separated region IDs

            Examples:
              EveOnTrader.Worker --sell-regions all --buy-regions all
              EveOnTrader.Worker --sell-regions 10000002,10000043 --buy-regions 10000002
              EveOnTrader.Worker --sell-regions 10000002 --buy-regions none

            Help:
              EveOnTrader.Worker --help
            """);
    }

    // TryReadOptionValues reads required sell and buy option values.
    private static bool TryReadOptionValues(
        string[] args,
        out string sellValue,
        out string buyValue,
        out string error)
    {
        sellValue = "";
        buyValue = "";
        error = "";

        var optionValues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        for (var index = 0; index < args.Length; index += 2)
        {
            var option = args[index];

            if (!option.Equals(SellRegionsOption, StringComparison.OrdinalIgnoreCase) &&
                !option.Equals(BuyRegionsOption, StringComparison.OrdinalIgnoreCase))
            {
                error = $"Unknown option: {option}";
                return false;
            }

            if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
            {
                error = $"Missing value for {option}.";
                return false;
            }

            var value = args[index + 1].Trim();

            if (string.IsNullOrWhiteSpace(value))
            {
                error = $"Missing value for {option}.";
                return false;
            }

            if (!optionValues.TryAdd(option, value))
            {
                error = $"Option specified more than once: {option}";
                return false;
            }
        }

        if (!optionValues.TryGetValue(SellRegionsOption, out var resolvedSellValue))
        {
            error = $"Missing required option: {SellRegionsOption}";
            return false;
        }

        if (!optionValues.TryGetValue(BuyRegionsOption, out var resolvedBuyValue))
        {
            error = $"Missing required option: {BuyRegionsOption}";
            return false;
        }

        sellValue = resolvedSellValue;
        buyValue = resolvedBuyValue;

        return true;
    }

    // TryResolveRegionSelection converts all, none, or region IDs into validated IDs.
    private static bool TryResolveRegionSelection(
        string value,
        string optionName,
        HashSet<long> availableRegionIds,
        List<long> allRegionIds,
        out string selectionName,
        out List<long> regionIds,
        out string error)
    {
        selectionName = "";
        regionIds = [];
        error = "";

        if (value.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            if (allRegionIds.Count == 0)
            {
                error = "No available regions found.";
                return false;
            }

            selectionName = "All";
            regionIds = [.. allRegionIds];
            return true;
        }

        if (value.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            selectionName = "None";
            return true;
        }

        var parts = value.Split(',', StringSplitOptions.TrimEntries);

        if (parts.Length == 0 || parts.Any(string.IsNullOrWhiteSpace))
        {
            error = $"Invalid region list for {optionName}: {value}";
            return false;
        }

        foreach (var part in parts)
        {
            if (!long.TryParse(part, out var regionId) || regionId <= 0)
            {
                error = $"Invalid region ID for {optionName}: {part}";
                return false;
            }

            regionIds.Add(regionId);
        }

        regionIds = regionIds
            .Distinct()
            .OrderBy(regionId => regionId)
            .ToList();

        var unknownRegionIds = regionIds
            .Where(regionId => !availableRegionIds.Contains(regionId))
            .ToList();

        if (unknownRegionIds.Count > 0)
        {
            error = $"Unknown region IDs for {optionName}: {string.Join(", ", unknownRegionIds)}";
            return false;
        }

        selectionName = string.Join(",", regionIds);
        return true;
    }

    // BuildSlices creates one full-region import slice per region and order side.
    private static List<MarketOrderImportSlice> BuildSlices(
        IReadOnlyList<long> regionIds,
        MarketOrderSide side)
    {
        return regionIds
            .Select(regionId => new MarketOrderImportSlice
            {
                RegionId = regionId,
                Side = side,
                TypeId = null
            })
            .ToList();
    }
}