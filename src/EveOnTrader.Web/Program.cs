using EveOnTrader.Core.DealFinding.Services;
using EveOnTrader.Infra;
using EveOnTrader.Infra.Data;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders =
        ForwardedHeaders.XForwardedFor |
        ForwardedHeaders.XForwardedProto;

    // ECS security group allows Web traffic only from ALB.
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
    options.ForwardLimit = 1;
});

var connectionString = builder.Configuration
    .GetConnectionString("EveDatabase")
    ?? throw new InvalidOperationException(
        "Connection string 'EveDatabase' is not configured.");

builder.Services.AddInfra(connectionString);

// Register Core deal finders
builder.Services.AddScoped<ItemRouteDealFinder>();
builder.Services.AddScoped<StationToStationMarketOrdersBuilder>();
builder.Services.AddScoped<StationToStationDealFinder>();
builder.Services.AddScoped<MarketDealFinder>();

var app = builder.Build();

app.UseForwardedHeaders();

// Ensure DB exists
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.EnsureCreatedAsync();
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapGet("/health", () => Results.Ok("Healthy"));

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();