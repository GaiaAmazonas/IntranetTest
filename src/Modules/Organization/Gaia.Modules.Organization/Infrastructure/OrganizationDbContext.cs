using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace Gaia.Modules.Organization.Infrastructure;

public sealed class OrganizationDbContext(
    DbContextOptions<OrganizationDbContext> options)
    : DbContext(options)
{
    public const string Schema = "organization";

    public DbSet<UnitType> UnitTypes => Set<UnitType>();
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<OrganizationalUnit> Units => Set<OrganizationalUnit>();
    public DbSet<Position> Positions => Set<Position>();
    public DbSet<OrganizationChange> Changes => Set<OrganizationChange>();

    public override async Task<int> SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        AddChangeRecords();
        return await base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);

        ConfigureAudited<UnitType>(modelBuilder, "unit_types");
        ConfigureAudited<Site>(modelBuilder, "sites");
        ConfigureAudited<OrganizationalUnit>(modelBuilder, "units");
        ConfigureAudited<Position>(modelBuilder, "positions");

        modelBuilder.Entity<OrganizationChange>(entity =>
        {
            entity.ToTable("changes");
            entity.HasKey(item => item.Id);
            entity.Property(item => item.EntityType).HasMaxLength(100);
            entity.Property(item => item.Action).HasMaxLength(20);
            entity.Property(item => item.Actor).HasMaxLength(256);
            entity.HasIndex(item => item.EntityId);
            entity.HasIndex(item => item.OccurredAtUtc);
        });

        modelBuilder.Entity<UnitType>(entity =>
        {
            entity.Property(item => item.Code).HasMaxLength(30);
            entity.Property(item => item.Name).HasMaxLength(150);
            entity.Property(item => item.ColorToken).HasMaxLength(50);
            entity.HasIndex(item => item.Code).IsUnique();
            entity.HasIndex(item => item.Name).IsUnique();
        });

        modelBuilder.Entity<Site>(entity =>
        {
            entity.Property(item => item.Code).HasMaxLength(30);
            entity.Property(item => item.Name).HasMaxLength(150);
            entity.Property(item => item.City).HasMaxLength(100);
            entity.Property(item => item.Address).HasMaxLength(250);
            entity.HasIndex(item => item.Code).IsUnique();
        });

        modelBuilder.Entity<OrganizationalUnit>(entity =>
        {
            entity.Property(item => item.Code).HasMaxLength(50);
            entity.Property(item => item.Name).HasMaxLength(200);
            entity.Property(item => item.ShortName).HasMaxLength(100);
            entity.HasIndex(item => item.Code).IsUnique();
            entity.HasOne(item => item.Parent)
                .WithMany(item => item.Children)
                .HasForeignKey(item => item.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.UnitType)
                .WithMany()
                .HasForeignKey(item => item.UnitTypeId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(item => item.Site)
                .WithMany()
                .HasForeignKey(item => item.SiteId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Position>(entity =>
        {
            entity.Property(item => item.Code).HasMaxLength(50);
            entity.Property(item => item.Name).HasMaxLength(150);
            entity.HasIndex(item => item.Code).IsUnique();
        });
    }

    private void AddChangeRecords()
    {
        var changes = ChangeTracker
            .Entries<AuditedEntity>()
            .Where(entry =>
                entry.State is EntityState.Added or EntityState.Modified)
            .Select(entry =>
            {
                var action = entry.State == EntityState.Added ? "created" : "updated";
                var before = entry.State == EntityState.Modified
                    ? entry.OriginalValues.Properties.ToDictionary(
                        property => property.Name,
                        property => entry.OriginalValues[property])
                    : null;
                var after = entry.CurrentValues.Properties.ToDictionary(
                    property => property.Name,
                    property => entry.CurrentValues[property]);
                var actor = entry.State == EntityState.Added
                    ? entry.Entity.CreatedBy
                    : entry.Entity.UpdatedBy ?? "unknown";

                return new OrganizationChange
                {
                    EntityType = entry.Metadata.ClrType.Name,
                    EntityId = entry.Entity.Id,
                    Action = action,
                    BeforeJson = before is null ? null : JsonSerializer.Serialize(before),
                    AfterJson = JsonSerializer.Serialize(after),
                    Actor = actor
                };
            })
            .ToList();

        Changes.AddRange(changes);
    }

    private static void ConfigureAudited<TEntity>(
        ModelBuilder builder,
        string tableName)
        where TEntity : AuditedEntity
    {
        builder.Entity<TEntity>(entity =>
        {
            entity.ToTable(tableName);
            entity.HasKey(item => item.Id);
            entity.Property(item => item.CreatedBy).HasMaxLength(256);
            entity.Property(item => item.UpdatedBy).HasMaxLength(256);
        });
    }
}

public sealed class OrganizationChange
{
    public long Id { get; init; }
    public required string EntityType { get; init; }
    public Guid EntityId { get; init; }
    public required string Action { get; init; }
    public string? BeforeJson { get; init; }
    public required string AfterJson { get; init; }
    public required string Actor { get; init; }
    public DateTimeOffset OccurredAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
