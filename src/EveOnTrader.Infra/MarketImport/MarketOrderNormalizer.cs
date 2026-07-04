using EveOnTrader.Core.Models;
using EveOnTrader.Infra.Esi.Models;

namespace EveOnTrader.Infra.MarketImport;

// MarketOrderNormalizer converts raw ESI market rows into local MarketOrder entities.
public static class MarketOrderNormalizer
{
    // Normalize maps one ESI market row to one DB market order and stamps import metadata.
    public static MarketOrder Normalize(
        EsiMarketRow row,
        long regionId,
        Guid importBatchId,
        DateTime importedAtUtc)
    {
        return new MarketOrder
        {
            OrderId = row.OrderId,
            IsBuyOrder = row.IsBuyOrder,
            Issued = row.Issued,
            LocationId = row.LocationId,
            SystemId = row.SystemId,
            TypeId = row.TypeId,
            Price = row.Price,
            VolumeRemain = row.VolumeRemain,
            VolumeTotal = row.VolumeTotal,
            MinVolume = row.MinVolume,
            Duration = row.Duration,
            Range = row.Range ?? "",

            RegionId = regionId,
            ImportBatchId = importBatchId,
            ImportedAtUtc = importedAtUtc
        };
    }
}