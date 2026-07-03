using System.Net.Http.Json;
using EveOnTrader.Infra.Esi.Models;

namespace EveOnTrader.Infra.Esi;

// EsiMarketClient downloads market order pages from ESI.
public class EsiMarketClient
{
    private readonly EsiHttpClient _esiHttpClient;

    // Creates market client with shared ESI HTTP wrapper.
    public EsiMarketClient(EsiHttpClient esiHttpClient)
    {
        _esiHttpClient = esiHttpClient;
    }

    // Downloads one market-order page and returns rows plus total page count.
    public async Task<EsiMarketOrdersPage> GetMarketOrdersPageAsync(
        long regionId,
        bool isBuyOrder,
        long? typeId,
        int page,
        CancellationToken cancellationToken = default)
    {
        var url = BuildMarketOrdersUrl(regionId, isBuyOrder, typeId, page);
        var operation = DescribeRequest(regionId, isBuyOrder, typeId, page);

        using var resp = await _esiHttpClient.GetAsync(url, operation, cancellationToken);

        var totalPages = GetTotalPages(resp);
        var rows = await resp.Content.ReadFromJsonAsync<List<EsiMarketRow>>(cancellationToken)
                   ?? [];

        return new EsiMarketOrdersPage
        {
            Rows = rows,
            TotalPages = totalPages
        };
    }

    // Builds ESI market-orders URL for one request slice and page number.
    private static string BuildMarketOrdersUrl(long regionId, bool isBuyOrder, long? typeId, int page)
    {
        var orderType = isBuyOrder ? "buy" : "sell";
        var url = $"markets/{regionId}/orders/?order_type={orderType}&datasource=tranquility&page={page}";

        if (typeId.HasValue)
        {
            url += $"&type_id={typeId.Value}";
        }

        return url;
    }

    // Returns readable label for ESI logs from one market request page.
    private static string DescribeRequest(long regionId, bool isBuyOrder, long? typeId, int page)
    {
        var orderType = isBuyOrder ? "buy" : "sell";

        if (typeId.HasValue)
        {
            return $"Region {regionId} {orderType} type {typeId.Value} page {page}";
        }

        return $"Region {regionId} {orderType} all-items page {page}";
    }

    // Reads ESI X-Pages header.
    private static int GetTotalPages(HttpResponseMessage resp)
    {
        if (resp.Headers.TryGetValues("X-Pages", out var values) &&
            int.TryParse(values.FirstOrDefault(), out var totalPages) &&
            totalPages > 0)
        {
            return totalPages;
        }

        return 1;
    }
}

// EsiMarketOrdersPage stores one market-order page plus ESI total page count.
public class EsiMarketOrdersPage
{
    public List<EsiMarketRow> Rows { get; set; } = [];
    public int TotalPages { get; set; }
}