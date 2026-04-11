using EveOnTrader.Infra.Queries;
using Microsoft.AspNetCore.Mvc;

namespace EveOnTrader.Web.Controllers;

public class OrdersController : Controller
{
    private readonly OrderQueryService _orderQueryService;

    public OrdersController(OrderQueryService orderQueryService)
    {
        _orderQueryService = orderQueryService;
    }

    // GET: /Orders
    public async Task<IActionResult> Index()
    {
        var rows = await _orderQueryService.GetLatestSellOrdersAsync(200);
        return View(rows);
    }
}