using System.Net.Http.Headers;
using EveOnTrader.Core.DealFinding.Services;
using EveOnTrader.Core.MarketImport;
using EveOnTrader.Core.RouteFinding;
using EveOnTrader.Infra.Data;
using EveOnTrader.Infra.Esi;
using EveOnTrader.Infra.MarketImport;
using EveOnTrader.Infra.Queries;
using EveOnTrader.Infra.RouteFinding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EveOnTrader.Infra;

public static class ServiceRegistration
{
    public static IServiceCollection AddInfra(this IServiceCollection services, string sqliteConnectionString)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(sqliteConnectionString));

        services.AddHttpClient<EsiHttpClient>(client =>
        {
            client.BaseAddress = new Uri("https://esi.evetech.net/latest/");

            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            client.DefaultRequestHeaders.TryAddWithoutValidation(
                "X-Compatibility-Date",
                "2025-12-16");

            client.DefaultRequestHeaders.UserAgent.ParseAdd("EveOnTrader/1.0");
        });

        services.AddScoped<EsiMarketClient>();
        services.AddScoped<EsiUniverseClient>();
        services.AddScoped<EsiDistanceClient>();

        services.AddScoped<UniverseReferenceSyncService>();
        services.AddScoped<ItemTypeRefSyncService>();
        services.AddScoped<MarketOrderImportWriter>();
        services.AddScoped<IMarketOrderImportService, MarketOrderImportService>();
        services.AddScoped<IRegionCatalogQuery, RegionCatalogQuery>();

        services.AddScoped<OrderQueryService>();
        services.AddScoped<OrderInRouteQueryService>();
        services.AddScoped<IStationToStationOrderRouteQuery, OrderInRouteQueryService>();
        services.AddScoped<IRegionToLocationsQuery, RegionToLocationsQuery>();
        services.AddScoped<IMultiLocationMarketOrderQuery, MultiLocationMarketOrderQuery>();
        services.AddScoped<ISingleItemStationToStationQuery, SingleItemStationToStationQuery>();
        services.AddScoped<IStationDistanceFinder, StationDistanceFinder>();

        return services;
    }
}