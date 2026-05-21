using EveOnTrader.Infra.Queries;
using Microsoft.AspNetCore.Mvc;

namespace EveOnTrader.Web.Controllers;

// OrdersController serves order list pages.
public class OrdersController : Controller
{
    private readonly OrderQueryService _orderQueryService;

    // Creates controller with order query service.
    public OrdersController(OrderQueryService orderQueryService)
    {
        _orderQueryService = orderQueryService;
    }

    // GET: /Orders
    // Loads latest order rows for web page.
    public async Task<IActionResult> Index()
    {
        var rows = await _orderQueryService.GetLatestOrdersAsync(200);
        return View(rows);
    }
}
