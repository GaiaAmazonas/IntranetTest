using Microsoft.EntityFrameworkCore;

namespace Gaia.Modules.Inventory.Infrastructure;

public sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    public const string Schema = "inventory";
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<InventoryItem> Items => Set<InventoryItem>();
    public DbSet<InventoryAssignment> Assignments => Set<InventoryAssignment>();
    public DbSet<InventoryMovement> Movements => Set<InventoryMovement>();
    public DbSet<InventoryImportIssue> ImportIssues => Set<InventoryImportIssue>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.Entity<Product>(e => { e.ToTable("products"); e.HasKey(x => x.Id); e.HasIndex(x => x.Code).IsUnique(); e.Property(x => x.Code).HasMaxLength(40); e.Property(x => x.Name).HasMaxLength(180); });
        modelBuilder.Entity<Brand>(e => { e.ToTable("brands"); e.HasKey(x => x.Id); e.HasIndex(x => x.Name).IsUnique(); e.Property(x => x.Name).HasMaxLength(120); });
        modelBuilder.Entity<InventoryItem>(e => { e.ToTable("items"); e.HasKey(x => x.Id); e.HasIndex(x => x.AssetCode).IsUnique(); e.HasIndex(x => x.SerialNumber); e.Property(x => x.Value).HasPrecision(18, 2); e.HasOne(x => x.Product).WithMany().HasForeignKey(x => x.ProductId); e.HasOne(x => x.Brand).WithMany().HasForeignKey(x => x.BrandId); });
        modelBuilder.Entity<InventoryAssignment>(e => { e.ToTable("assignments"); e.HasKey(x => x.Id); e.HasOne(x => x.InventoryItem).WithMany().HasForeignKey(x => x.InventoryItemId); e.HasIndex(x => new { x.InventoryItemId, x.IsActive }).HasFilter("\"IsActive\" = TRUE").IsUnique(); });
        modelBuilder.Entity<InventoryMovement>(e => { e.ToTable("movements"); e.HasKey(x => x.Id); e.HasOne(x => x.InventoryItem).WithMany().HasForeignKey(x => x.InventoryItemId); e.HasIndex(x => new { x.InventoryItemId, x.OccurredAtUtc }); });
        modelBuilder.Entity<InventoryImportIssue>(e => { e.ToTable("import_issues"); e.HasKey(x => x.Id); e.HasIndex(x => x.BatchId); });
    }
}
