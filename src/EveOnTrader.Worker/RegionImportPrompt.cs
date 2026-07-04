using EveOnTrader.Core.MarketImport;
using EveOnTrader.Core.Models;

namespace EveOnTrader.Worker;

// RegionImportPrompt asks user for sell and buy region presets, then converts choices into import request.
public static class RegionImportPrompt
{
    private static readonly RegionPreset[] Presets =
    [
        new("All", []),
        new("The Forge", [10000002]),
        new("The Forge + Domain (Amarr)", [10000002, 10000043]),
        new("The Forge + Sinq Laison (Dodixie)", [10000002, 10000032]),
        new("The Forge + Heimatar (Rens)", [10000002, 10000030]),
        new("The Forge + Metropolis (Hek)", [10000002, 10000042])
    ];

    // Shows sell and buy preset menus, then returns combined import request.
    public static MarketOrderImportRequest Prompt(IReadOnlyList<Region> availableRegions)
    {
        var sellSelection = PromptOrderSideSelection(availableRegions, "sell", MarketOrderSide.Sell);
        var buySelection = PromptOrderSideSelection(availableRegions, "buy", MarketOrderSide.Buy);

        return new MarketOrderImportRequest
        {
            SelectionName = $"Sell: {sellSelection.SelectionName} | Buy: {buySelection.SelectionName}",
            Slices = sellSelection.Slices
                .Concat(buySelection.Slices)
                .ToList()
        };
    }

    // Shows preset menu for one order side and returns matching import slices.
    private static OrderSideSelection PromptOrderSideSelection(
        IReadOnlyList<Region> availableRegions,
        string orderLabel,
        MarketOrderSide side)
    {
        var availableRegionIds = availableRegions
            .Select(x => x.RegionId)
            .ToHashSet();

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine($"Choose region preset for {orderLabel} orders:");
            Console.WriteLine("0. None");
            Console.WriteLine("1. All");
            Console.WriteLine("2. The Forge");
            Console.WriteLine("3. The Forge + Domain (Amarr)");
            Console.WriteLine("4. The Forge + Sinq Laison (Dodixie)");
            Console.WriteLine("5. The Forge + Heimatar (Rens)");
            Console.WriteLine("6. The Forge + Metropolis (Hek)");
            Console.WriteLine("7. Manual region IDs");
            Console.Write("Selection: ");

            var input = (Console.ReadLine() ?? "").Trim();

            //if no import option selected, return empty slice list
            if (input == "0")
            {
                return new OrderSideSelection
                {
                    SelectionName = "None",
                    Slices = []
                };
            }

            //if manual option selected, prompt for region IDs and validate them
            if (input == "7")
            {
                var manual = PromptManual(availableRegionIds, orderLabel, side);

                if (manual is not null)
                {
                    return manual;
                }

                continue;
            }

            //validate preset selection
            if (!int.TryParse(input, out var presetNumber) || presetNumber < 1 || presetNumber > Presets.Length)
            {
                Console.WriteLine("Invalid selection.");
                continue;
            }

            var preset = Presets[presetNumber - 1];

            //if preset has no region IDs, use all available regions
            if (preset.RegionIds.Count == 0)
            {
                var regionIds = availableRegions
                    .Select(x => x.RegionId)
                    .OrderBy(x => x)
                    .ToList();

                return new OrderSideSelection
                {
                    SelectionName = preset.Name,
                    Slices = BuildSlices(regionIds, side)
                };
            }

            //validate that all preset region IDs are available
            var missingPresetIds = preset.RegionIds
                .Where(id => !availableRegionIds.Contains(id))
                .ToList();

            if (missingPresetIds.Count > 0)
            {
                Console.WriteLine($"Preset contains unknown region IDs: {string.Join(", ", missingPresetIds)}");
                continue;
            }

            return new OrderSideSelection
            {
                SelectionName = preset.Name,
                Slices = BuildSlices(preset.RegionIds, side)
            };
        }
    }

    //prompts user for region IDs, validates them, and returns selection object if valid
    private static OrderSideSelection? PromptManual(
        HashSet<long> availableRegionIds,
        string orderLabel,
        MarketOrderSide side)
    {
        Console.Write($"Enter region IDs for {orderLabel} orders separated by commas: ");
        var input = (Console.ReadLine() ?? "").Trim();

        if (string.IsNullOrWhiteSpace(input))
        {
            Console.WriteLine("No region IDs entered.");
            return null;
        }

        var parts = input.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var parsedIds = new List<long>();

        foreach (var part in parts)
        {
            if (!long.TryParse(part, out var regionId))
            {
                Console.WriteLine($"Invalid region ID: {part}");
                return null;
            }

            parsedIds.Add(regionId);
        }

        var regionIds = parsedIds
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        //validate that all entered region IDs exist
        var unknownIds = regionIds
            .Where(id => !availableRegionIds.Contains(id))
            .ToList();

        if (unknownIds.Count > 0)
        {
            Console.WriteLine($"Unknown region IDs: {string.Join(", ", unknownIds)}");
            return null;
        }

        return new OrderSideSelection
        {
            SelectionName = "Manual",
            Slices = BuildSlices(regionIds, side)
        };
    }

    // Builds import slices for given region IDs and order side.
    private static List<MarketOrderImportSlice> BuildSlices(IReadOnlyList<long> regionIds, MarketOrderSide side)
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

    // OrderSideSelection stores one side's menu choice and resulting import slices.
    private sealed class OrderSideSelection
    {
        public string SelectionName { get; set; } = "";
        public List<MarketOrderImportSlice> Slices { get; set; } = [];
    }

    // RegionPreset stores one menu preset name and region ID list.
    private sealed class RegionPreset
    {
        // Creates one region preset entry.
        public RegionPreset(string name, IReadOnlyList<long> regionIds)
        {
            Name = name;
            RegionIds = regionIds;
        }

        public string Name { get; }
        public IReadOnlyList<long> RegionIds { get; }
    }
}