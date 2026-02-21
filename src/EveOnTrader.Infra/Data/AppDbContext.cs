using EveOnTrader.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace EveOnTrader.Infra.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<MarketOrder> MarketOrders => Set<MarketOrder>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<MarketOrder>(e =>
        {
            e.HasKey(x => x.OrderId);

            // Optional but recommended: keep RegionId + TypeId queries fast later
            e.HasIndex(x => x.RegionId);
            e.HasIndex(x => x.TypeId);
            e.HasIndex(x => x.LocationId);
        });
    }
}