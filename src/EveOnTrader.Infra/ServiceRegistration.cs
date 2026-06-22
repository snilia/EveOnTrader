using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using EveOnTrader.Core.DealFinding.Services;
using EveOnTrader.Core.RouteFinding;
using EveOnTrader.Infra.Data;
using EveOnTrader.Infra.Queries;
using EveOnTrader.Infra.RouteFinding;

namespace EveOnTrader.Infra;

public static class ServiceRegistration
{
    public static IServiceCollection AddInfra(this IServiceCollection services, string sqliteConnectionString)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(sqliteConnectionString));

        services.AddScoped<OrderQueryService>();
        services.AddScoped<OrderInRouteQueryService>();
        services.AddScoped<IStationToStationOrderRouteQuery, OrderInRouteQueryService>();
        services.AddScoped<IRouteDistanceService, RouteDistanceService>();

        return services;
    }
}
//its for dependancy injection (lets other classes recieve an AppDbContext instead of creating it. makes it easier and the same for both web and console thingies i'll have.
//AddInfra is an extension method, makes the thingy that "creates" the context using similar word/path builder.Services.AddInfra(...

//AddInfra is an extension method that registers your infrastructure services (like AppDbContext) into the DI container, so Web/Worker can request AppDbContext later without manually constructing it.