using EveOnTrader.Core.DealFinding.Models;
using EveOnTrader.Core.ReadModels;

namespace EveOnTrader.Core.DealFinding.Services;

// ItemRouteDealFinder  match cheapest sells against highest buys, chunk by chunk, build cumulative steps, until step not profitable
public class ItemRouteDealFinder
{
    // FindItemDeals walks cheapest source sells against highest destination buys and builds cumulative deal steps.
    public ItemDealResult? FindItemDeals(SingleItemTypeOrderRoute itemRoute, DealFinderOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(itemRoute);

        options ??= new DealFinderOptions();

        var jumpCount = options.JumpCount > 0 ? options.JumpCount : 1;


        // check for existence of orders before trying to find deals, and return null if no deals possible
        if (itemRoute.SourceSellOrders.Count == 0 || itemRoute.DestinationBuyOrders.Count == 0)
        {
            return null;
        }

        if (options.MaxSteps.HasValue && options.MaxSteps.Value <= 0)
        {
            return null;
        }


        // quick check if even the best possible deal would be profitable
        var bestSourceSellPrice = itemRoute.SourceSellOrders[0].Price;
        var bestDestinationBuyPrice = itemRoute.DestinationBuyOrders[0].Price;
        var bestDestinationSellRevenuePerUnit = bestDestinationBuyPrice * (1m - options.SalesTaxRate);

        if (bestDestinationSellRevenuePerUnit <= bestSourceSellPrice)
        {
            return null;
        }


        var result = new ItemDealResult
        {
            TypeId = itemRoute.TypeId,
            TypeName = itemRoute.TypeName,
            UnitVolumeM3 = itemRoute.UnitVolumeM3,
            SourceSellOrderCount = itemRoute.SourceSellOrders.Count,
            DestinationBuyOrderCount = itemRoute.DestinationBuyOrders.Count,
            BestSourceSellPrice = itemRoute.SourceSellOrders.FirstOrDefault()?.Price,
            BestDestinationBuyPrice = itemRoute.DestinationBuyOrders.FirstOrDefault()?.Price
        };

        var sellIndex = 0;
        var buyIndex = 0;
        var stepNumber = 0;

        var sellRemaining = itemRoute.SourceSellOrders[sellIndex].VolumeRemain;
        var buyRemaining = itemRoute.DestinationBuyOrders[buyIndex].VolumeRemain;

        long totalUnits = 0;
        decimal totalBuyCost = 0m;
        decimal totalSellRevenue = 0m;
        decimal totalVolumeM3 = 0m;

        while (sellIndex < itemRoute.SourceSellOrders.Count && buyIndex < itemRoute.DestinationBuyOrders.Count)
        {
            if (options.MaxSteps.HasValue && stepNumber >= options.MaxSteps.Value)
            {
                break;
            }

            var sellOrder = itemRoute.SourceSellOrders[sellIndex];
            var buyOrder = itemRoute.DestinationBuyOrders[buyIndex];

            var buyCostPerUnit = sellOrder.Price;

            // Current route matcher buys from source sell orders and sells into destination buy orders.
            // That path pays sales tax, but not broker fee.
            var sellRevenuePerUnit = buyOrder.Price * (1m - options.SalesTaxRate);

            if (sellRevenuePerUnit <= buyCostPerUnit)
            {
                break;
            }

            //make sure not to exceed user defined limits with this step's matched units, even if more units available in orders
            var matchedUnits = Math.Min(sellRemaining, buyRemaining);
            matchedUnits = ClampUnitsToLimits(
                matchedUnits,
                totalBuyCost,
                totalVolumeM3,
                itemRoute.UnitVolumeM3,
                buyCostPerUnit,
                options);

            if (matchedUnits <= 0)
            {
                break;
            }


            // this step is good, add it to the deal result and move forward
            stepNumber++;

            var lastChunkBuyCost = matchedUnits * buyCostPerUnit;
            var lastChunkSellRevenue = matchedUnits * sellRevenuePerUnit;
            var lastChunkProfit = lastChunkSellRevenue - lastChunkBuyCost;
            var lastChunkVolumeM3 = matchedUnits * itemRoute.UnitVolumeM3;

            totalUnits += matchedUnits;
            totalBuyCost += lastChunkBuyCost;
            totalSellRevenue += lastChunkSellRevenue;
            totalVolumeM3 += lastChunkVolumeM3;

            var totalProfit = totalSellRevenue - totalBuyCost;

            var step = new ItemDealStep
            {
                StepNumber = stepNumber,

                LastChunkUnits = matchedUnits,
                LastChunkBuyCost = lastChunkBuyCost,
                LastChunkSellRevenue = lastChunkSellRevenue,
                LastChunkProfit = lastChunkProfit,
                LastChunkVolumeM3 = lastChunkVolumeM3,
                LastChunkRoi = lastChunkBuyCost > 0m ? lastChunkProfit / lastChunkBuyCost : 0m,
                LastChunkProfitPerVolumeM3 = lastChunkVolumeM3 > 0m ? lastChunkProfit / lastChunkVolumeM3 : 0m,
                LastChunkProfitPerJump = lastChunkProfit / jumpCount,

                TotalUnits = totalUnits,
                TotalBuyCost = totalBuyCost,
                TotalSellRevenue = totalSellRevenue,
                TotalProfit = totalProfit,
                TotalVolumeM3 = totalVolumeM3,
                TotalRoi = totalBuyCost > 0m ? totalProfit / totalBuyCost : 0m,
                TotalProfitPerVolumeM3 = totalVolumeM3 > 0m ? totalProfit / totalVolumeM3 : 0m,
                TotalProfitPerJump = totalProfit / jumpCount
            };

            // check if this step meets user defined roi limits,
            if (options.MinTotalRoi.HasValue && step.TotalRoi < options.MinTotalRoi.Value)
            {
                break;
            }

            result.Steps.Add(step);

            // reduce remaining volumes in current orders by matched units, and move to next order if fully matched
            sellRemaining -= matchedUnits;
            buyRemaining -= matchedUnits;

            if (sellRemaining == 0)
            {
                sellIndex++;

                if (sellIndex < itemRoute.SourceSellOrders.Count)
                {
                    sellRemaining = itemRoute.SourceSellOrders[sellIndex].VolumeRemain;
                }
            }

            if (buyRemaining == 0)
            {
                buyIndex++;

                if (buyIndex < itemRoute.DestinationBuyOrders.Count)
                {
                    buyRemaining = itemRoute.DestinationBuyOrders[buyIndex].VolumeRemain;
                }
            }
        }

        return result.Steps.Count > 0 ? result : null;
    }

    // ClampUnitsToLimits reduces matched units so current step stays inside optional cost and volume caps.
    private static long ClampUnitsToLimits(
        long matchedUnits,
        decimal totalBuyCost,
        decimal totalVolumeM3,
        decimal unitVolumeM3,
        decimal buyCostPerUnit,
        DealFinderOptions options)
    {
        var limitedUnits = matchedUnits;

        // for max cost
        if (options.MaxTotalBuyCost.HasValue)
        {
            var remainingBuyCost = options.MaxTotalBuyCost.Value - totalBuyCost;

            if (remainingBuyCost <= 0m)
            {
                return 0;
            }

            var maxUnitsByBuyCost = (long)decimal.Floor(remainingBuyCost / buyCostPerUnit);
            limitedUnits = Math.Min(limitedUnits, maxUnitsByBuyCost);
        }

        // for max volume
        if (options.MaxTotalVolumeM3.HasValue && unitVolumeM3 > 0m)
        {
            var remainingVolumeM3 = options.MaxTotalVolumeM3.Value - totalVolumeM3;

            if (remainingVolumeM3 <= 0m)
            {
                return 0;
            }

            var maxUnitsByVolumeM3 = (long)decimal.Floor(remainingVolumeM3 / unitVolumeM3);
            limitedUnits = Math.Min(limitedUnits, maxUnitsByVolumeM3);
        }

        return Math.Max(0, limitedUnits);
    }
}
