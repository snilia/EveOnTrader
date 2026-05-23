using EveOnTrader.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace EveOnTrader.Infra.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<MarketOrder> MarketOrders => Set<MarketOrder>();
    public DbSet<ItemTypeRef> ItemTypeRefs => Set<ItemTypeRef>();
    public DbSet<Region> Regions => Set<Region>();
    public DbSet<SolarSystem> SolarSystems => Set<SolarSystem>();
    public DbSet<MarketLocation> MarketLocations => Set<MarketLocation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<MarketOrder>(e =>
        {
            e.HasKey(x => x.OrderId);
            e.Property(x => x.OrderId).ValueGeneratedNever();

            //keep deal-finder and scoped refresh queries fast
            e.HasIndex(x => new { x.RegionId, x.IsBuyOrder, x.ImportedAtUtc, x.TypeId });
            e.HasIndex(x => new { x.LocationId, x.IsBuyOrder, x.ImportedAtUtc });
        });

        modelBuilder.Entity<ItemTypeRef>(e =>
        {
            e.HasKey(x => x.TypeId);
            e.Property(x => x.TypeId).ValueGeneratedNever();

            e.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(300);

            e.Property(x => x.VolumeM3)
                .HasPrecision(18, 4);
        });

        modelBuilder.Entity<Region>(e =>
        {
            e.HasKey(x => x.RegionId);
            e.Property(x => x.RegionId).ValueGeneratedNever();

            e.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(300);
        });

        modelBuilder.Entity<SolarSystem>(e =>
        {
            e.HasKey(x => x.SolarSystemId);
            e.Property(x => x.SolarSystemId).ValueGeneratedNever();

            e.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(300);

            e.HasIndex(x => x.RegionId);
        });

        modelBuilder.Entity<MarketLocation>(e =>
        {
            e.HasKey(x => x.LocationId);
            e.Property(x => x.LocationId).ValueGeneratedNever();

            e.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(300);

            e.HasIndex(x => x.SolarSystemId);
        });

    }
}
