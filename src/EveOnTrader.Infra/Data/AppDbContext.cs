using EveOnTrader.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace EveOnTrader.Infra.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<MarketOrder> MarketOrders => Set<MarketOrder>();
    public DbSet<ItemTypeRef> ItemTypeRefs => Set<ItemTypeRef>();
    public DbSet<UniverseEntityRef> UniverseEntityRefs => Set<UniverseEntityRef>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<MarketOrder>(e =>
        {
            e.HasKey(x => x.OrderId);
            e.Property(x => x.OrderId).ValueGeneratedNever();

            //keep RegionId + TypeId queries fast later
            e.HasIndex(x => x.RegionId);
            e.HasIndex(x => x.TypeId);
            e.HasIndex(x => x.LocationId);
        });

        modelBuilder.Entity<ItemTypeRef>(e =>
        {
            e.HasKey(x => x.TypeId);
            e.Property(x => x.TypeId).ValueGeneratedNever();

            e.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(300);
        });

        modelBuilder.Entity<UniverseEntityRef>(e =>
        {
            e.HasKey(x => x.EntityId);
            e.Property(x => x.EntityId).ValueGeneratedNever();

            e.Property(x => x.Category)
                .IsRequired()
                .HasMaxLength(100);

            e.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(300);
        });
    }
}