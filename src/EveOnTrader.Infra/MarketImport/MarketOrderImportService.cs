using System.Diagnostics;
using EveOnTrader.Core.MarketImport;
using EveOnTrader.Infra.Esi;
using Microsoft.Extensions.Logging;

namespace EveOnTrader.Infra.MarketImport;

// MarketOrderImportService coordinates one full ESI market import run.
public class MarketOrderImportService : IMarketOrderImportService
{
    private readonly EsiMarketClient _esiMarketClient;
    private readonly MarketOrderImportWriter _marketOrderImportWriter;
    private readonly UniverseReferenceSyncService _universeReferenceSyncService;
    private readonly ItemTypeRefSyncService _itemTypeRefSyncService;
    private readonly ILogger<MarketOrderImportService> _logger;

    // Creates import service with ESI clients and supporting DB sync services.
    public MarketOrderImportService(
        EsiMarketClient esiMarketClient,
        MarketOrderImportWriter marketOrderImportWriter,
        UniverseReferenceSyncService universeReferenceSyncService,
        ItemTypeRefSyncService itemTypeRefSyncService,
        ILogger<MarketOrderImportService> logger)
    {
        _esiMarketClient = esiMarketClient;
        _marketOrderImportWriter = marketOrderImportWriter;
        _universeReferenceSyncService = universeReferenceSyncService;
        _itemTypeRefSyncService = itemTypeRefSyncService;
        _logger = logger;
    }

    // Runs reference sync, imports selected market order slices, then resolves locations and item refs.
    public async Task<MarketOrderImportResult> ImportAsync(
        MarketOrderImportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var sw = Stopwatch.StartNew();
        var importBatchId = Guid.NewGuid();
        var importedAtUtc = DateTime.UtcNow;

        var normalizedSlices = NormalizeSlices(request.Slices);
        ValidateNoOverlappingSlices(normalizedSlices);

        var result = new MarketOrderImportResult
        {
            ImportBatchId = importBatchId,
            ImportedAtUtc = importedAtUtc,
            SelectionName = request.SelectionName,
            RequestCount = request.Slices.Count,
            NormalizedRequestCount = normalizedSlices.Count
        };

        _logger.LogInformation(
            "Starting market import {ImportBatchId}. Selection: {SelectionName}. Slices: {SliceCount:n0}.",
            importBatchId,
            request.SelectionName,
            normalizedSlices.Count);

        var universeResult = await _universeReferenceSyncService.SyncAsync(cancellationToken);
        result.RegionsInserted = universeResult.RegionsInserted;
        result.SolarSystemsInserted = universeResult.SolarSystemsInserted;

        var orderCounts = await _marketOrderImportWriter.WithFastImportSettingsAsync(
            () => ImportMarketOrdersAsync(
                normalizedSlices,
                importBatchId,
                importedAtUtc,
                cancellationToken));

        result.DeletedMarketOrderCount = orderCounts.DeletedCount;
        result.InsertedMarketOrderCount = orderCounts.InsertedCount;

        result.MarketLocationsInserted = await _universeReferenceSyncService.SyncMarketLocationsAsync(cancellationToken);
        result.ItemTypeRefsInserted = await _itemTypeRefSyncService.SyncItemTypeRefsAsync(cancellationToken);

        sw.Stop();
        result.Elapsed = sw.Elapsed;

        result.Messages.Add(
            $"Imported {result.InsertedMarketOrderCount:n0} market orders in {result.Elapsed}.");

        _logger.LogInformation(
            "Finished market import {ImportBatchId}. Inserted {InsertedCount:n0} orders. Deleted {DeletedCount:n0} orders. Elapsed {Elapsed}.",
            importBatchId,
            result.InsertedMarketOrderCount,
            result.DeletedMarketOrderCount,
            result.Elapsed);

        return result;
    }

    // Imports all selected order slices and stamps each row with one batch ID and import time.
    private async Task<MarketOrderImportCounts> ImportMarketOrdersAsync(
        IReadOnlyList<MarketOrderImportSlice> slices,
        Guid importBatchId,
        DateTime importedAtUtc,
        CancellationToken cancellationToken)
    {
        long totalDeleted = 0;
        long totalInserted = 0;

        foreach (var slice in slices)
        {
            var sliceLabel = DescribeSlice(slice);

            _logger.LogInformation("Refreshing {SliceLabel}...", sliceLabel);

            totalDeleted += await _marketOrderImportWriter.DeleteExistingScopeAsync(
                slice,
                cancellationToken);

            var totalPages = 1;
            var insertedForSlice = 0;
            var seenOrderIds = new HashSet<long>();

            for (var page = 1; page <= totalPages; page++)
            {
                var pageResult = await _esiMarketClient.GetMarketOrdersPageAsync(
                    slice.RegionId,
                    slice.Side == MarketOrderSide.Buy,
                    slice.TypeId,
                    page,
                    cancellationToken);

                totalPages = pageResult.TotalPages;

                var insertedForPage = await _marketOrderImportWriter.InsertPageAsync(
                    pageResult.Rows,
                    slice,
                    importBatchId,
                    importedAtUtc,
                    seenOrderIds,
                    cancellationToken);

                insertedForSlice += insertedForPage;
                totalInserted += insertedForPage;

                _logger.LogInformation(
                    "{SliceLabel} page {Page:n0}/{TotalPages:n0}: inserted {InsertedForPage:n0} (raw {RawCount:n0}, total {InsertedForSlice:n0})",
                    sliceLabel,
                    page,
                    totalPages,
                    insertedForPage,
                    pageResult.Rows.Count,
                    insertedForSlice);
            }
        }

        return new MarketOrderImportCounts
        {
            DeletedCount = totalDeleted,
            InsertedCount = totalInserted
        };
    }

    // Removes exact duplicate slices so one scope is not imported twice in same run.
    private static List<MarketOrderImportSlice> NormalizeSlices(List<MarketOrderImportSlice> slices)
    {
        return slices
            .Where(x => x.RegionId > 0)
            .GroupBy(x => new { x.RegionId, x.Side, x.TypeId })
            .Select(x => x.First())
            .OrderBy(x => x.RegionId)
            .ThenBy(x => x.Side)
            .ThenBy(x => x.TypeId)
            .ToList();
    }

    // Blocks overlapping slices like full-region plus one-item same-side imports in same run.
    private static void ValidateNoOverlappingSlices(IReadOnlyList<MarketOrderImportSlice> slices)
    {
        var groups = slices
            .GroupBy(x => new { x.RegionId, x.Side });

        foreach (var group in groups)
        {
            if (group.Any(x => x.TypeId is null) && group.Count() > 1)
            {
                var orderType = group.Key.Side == MarketOrderSide.Buy ? "buy" : "sell";

                throw new InvalidOperationException(
                    $"Overlapping import requests not supported for region {group.Key.RegionId} {orderType}. Use either full-region or per-item requests, not both in same run.");
            }
        }
    }

    // Returns readable label for logs from one import slice.
    private static string DescribeSlice(MarketOrderImportSlice slice)
    {
        var orderType = slice.Side == MarketOrderSide.Buy ? "buy" : "sell";

        if (slice.TypeId.HasValue)
        {
            return $"Region {slice.RegionId} {orderType} type {slice.TypeId.Value}";
        }

        return $"Region {slice.RegionId} {orderType} all-items";
    }

    // MarketOrderImportCounts stores delete/insert counters for market orders.
    private sealed class MarketOrderImportCounts
    {
        public long DeletedCount { get; set; }
        public long InsertedCount { get; set; }
    }
}