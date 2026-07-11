using EveOnTrader.Core.DealFinding.Services;
using EveOnTrader.Infra;
using EveOnTrader.Infra.Data;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

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
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();