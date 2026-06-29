using EveOnTrader.Core.DealFinding.Services;
using EveOnTrader.Core.ReadModels;
using EveOnTrader.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace EveOnTrader.Web.Controllers;

// MarketDealDetailsController shows one item route's detailed order books.
public class MarketDealDetailsController : Controller
{
    private const int MaxDisplayedOrdersPerBook = 10;

    private readonly ISingleItemStationToStationQuery _singleItemStationToStationQuery;

    // Creates details controller with single-item station-to-station query.
    public MarketDealDetailsController(ISingleItemStationToStationQuery singleItemStationToStationQuery)
    {
        _singleItemStationToStationQuery = singleItemStationToStationQuery;
    }

    // GET: /MarketDealDetails
    public async Task<IActionResult> Index(
        long typeId,
        long sourceLocationId,
        long destinationLocationId,
        decimal salesTaxRate = 0.0337m,
        decimal brokerFeeRate = 0.012m)
    {
        if (typeId <= 0 || sourceLocationId <= 0 || destinationLocationId <= 0)
        {
            return BadRequest("typeId, sourceLocationId, and destinationLocationId are required.");
        }

        salesTaxRate = Math.Max(0m, salesTaxRate);
        brokerFeeRate = Math.Max(0m, brokerFeeRate);

        var orders = await _singleItemStationToStationQuery.GetOrdersAsync(
            typeId,
            sourceLocationId,
            destinationLocationId);

        var model = new MarketDealDetailsViewModel
        {
            TypeId = orders.TypeId,
            TypeName = orders.TypeName,
            UnitVolumeM3 = orders.UnitVolumeM3,

            SourceLocationId = orders.SourceLocationId,
            SourceLocationName = orders.SourceLocationName,
            SourceRegionId = orders.SourceRegionId,
            SourceRegionName = orders.SourceRegionName,

            DestinationLocationId = orders.DestinationLocationId,
            DestinationLocationName = orders.DestinationLocationName,
            DestinationRegionId = orders.DestinationRegionId,
            DestinationRegionName = orders.DestinationRegionName,

            SalesTaxRate = salesTaxRate,
            BrokerFeeRate = brokerFeeRate,

            SourceSellOrders = MapOrderRows(
                orders.SourceSellOrders,
                orders.UnitVolumeM3,
                salesTaxRate,
                brokerFeeRate),

            SourceBuyOrders = MapOrderRows(
                orders.SourceBuyOrders,
                orders.UnitVolumeM3,
                salesTaxRate,
                brokerFeeRate),

            DestinationSellOrders = MapOrderRows(
                orders.DestinationSellOrders,
                orders.UnitVolumeM3,
                salesTaxRate,
                brokerFeeRate),

            DestinationBuyOrders = MapOrderRows(
                orders.DestinationBuyOrders,
                orders.UnitVolumeM3,
                salesTaxRate,
                brokerFeeRate)
        };

        return View(model);
    }

    // MapOrderRows maps order rows to display rows with cumulative quantity.
    private static List<MarketDealDetailsOrderRowViewModel> MapOrderRows(
        List<ItemOrderRow> orders,
        decimal unitVolumeM3,
        decimal salesTaxRate,
        decimal brokerFeeRate)
    {
        var rows = new List<MarketDealDetailsOrderRowViewModel>();
        long cumulativeVolumeRemain = 0;

        foreach (var order in orders.Take(MaxDisplayedOrdersPerBook))
        {
            cumulativeVolumeRemain += order.VolumeRemain;

            rows.Add(new MarketDealDetailsOrderRowViewModel
            {
                OrderId = order.OrderId,
                Price = order.Price,
                NetImmediateSellPrice = order.Price * (1m - salesTaxRate),
                NetSellOrderPrice = order.Price * (1m - salesTaxRate - brokerFeeRate),
                VolumeRemain = order.VolumeRemain,
                VolumeTotal = order.VolumeTotal,
                MinVolume = order.MinVolume,
                Range = order.Range,
                Issued = order.Issued,
                CumulativeVolumeRemain = cumulativeVolumeRemain,
                CumulativeVolumeM3 = cumulativeVolumeRemain * unitVolumeM3
            });
        }

        return rows;
    }
}