using EveOnTrader.Core.Models;
using EveOnTrader.Worker.Models;

namespace EveOnTrader.Worker;

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

    public static MarketImportOptions Prompt(IReadOnlyList<Region> availableRegions)
    {
        var availableRegionIds = availableRegions
            .Select(x => x.RegionId)
            .ToHashSet();

        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("Choose region preset:");
            Console.WriteLine("1. All");
            Console.WriteLine("2. The Forge");
            Console.WriteLine("3. The Forge + Domain (Amarr)");
            Console.WriteLine("4. The Forge + Sinq Laison (Dodixie)");
            Console.WriteLine("5. The Forge + Heimatar (Rens)");
            Console.WriteLine("6. The Forge + Metropolis (Hek)");
            Console.WriteLine("7. Manual region IDs");
            Console.Write("Selection: ");

            var input = (Console.ReadLine() ?? "").Trim();

            //if manual option selected, prompt for region IDs and validate them
            if (input == "7")
            {
                var manual = PromptManual(availableRegionIds);

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
                return new MarketImportOptions
                {
                    SelectionName = preset.Name,
                    RegionIds = availableRegions
                        .Select(x => x.RegionId)
                        .OrderBy(x => x)
                        .ToList()
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

            //wrap chosen preset into object runner can use later
            return new MarketImportOptions
            {
                SelectionName = preset.Name,
                RegionIds = preset.RegionIds
            };
        }
    }

    //prompts user for region IDs, validates them, and returns options object if valid
    private static MarketImportOptions? PromptManual(HashSet<long> availableRegionIds)
    {
        Console.Write("Enter region IDs separated by commas: ");
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

        return new MarketImportOptions
        {
            SelectionName = "Manual",
            RegionIds = regionIds
        };
    }

    private sealed class RegionPreset
    {
        public RegionPreset(string name, IReadOnlyList<long> regionIds)
        {
            Name = name;
            RegionIds = regionIds;
        }

        public string Name { get; }
        public IReadOnlyList<long> RegionIds { get; }
    }
}
