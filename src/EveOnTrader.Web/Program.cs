using EveOnTrader.Core.DealFinding.Services;
using EveOnTrader.Infra;
using EveOnTrader.Infra.Data;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Same DB path as Worker: %LOCALAPPDATA%\EveOnTrader\eve.db
var dataDir = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "EveOnTrader");
Directory.CreateDirectory(dataDir);

var dbPath = Path.Combine(dataDir, "eve.db");
var connStr = $"Data Source={dbPath}";

// Helpful for debugging "different DB file" issues
Console.WriteLine($"Web DB Path: {dbPath}");

// Register Infra (DbContext, etc.)
builder.Services.AddInfra(connStr);

// Register Core deal finders
builder.Services.AddScoped<ItemRouteDealFinder>();
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