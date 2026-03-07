using EveOnTrader.Infra.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EveOnTrader.Web.Controllers;

public class OrdersController : Controller
{
    private readonly AppDbContext _db;

    public OrdersController(AppDbContext db)
    {
        _db = db;
    }

    // GET: /Orders
    public async Task<IActionResult> Index()
    {
        // Pull a small, recent slice so we don’t load huge tables into memory.
        var rows = await _db.MarketOrders
            .AsNoTracking()  //readonly, faster queries
            .OrderByDescending(o => o.Issued)
            .Take(200)
            .ToListAsync();

        return View(rows);
    }
}