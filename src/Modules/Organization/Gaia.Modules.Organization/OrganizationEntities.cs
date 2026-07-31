namespace Gaia.Modules.Organization;

public abstract class AuditedEntity
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public required string CreatedBy { get; init; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public string? UpdatedBy { get; set; }
}

public sealed class UnitType : AuditedEntity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required string ColorToken { get; set; }
    public int VisualOrder { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Site : AuditedEntity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? City { get; set; }
    public string? Address { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class OrganizationalUnit : AuditedEntity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? ShortName { get; set; }
    public Guid UnitTypeId { get; set; }
    public UnitType? UnitType { get; set; }
    public Guid? ParentId { get; set; }
    public OrganizationalUnit? Parent { get; set; }
    public ICollection<OrganizationalUnit> Children { get; } = [];
    public Guid? SiteId { get; set; }
    public Site? Site { get; set; }
    public int Level { get; set; }
    public string? Description { get; set; }
    public int VisualOrder { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class Position : AuditedEntity
{
    public required string Code { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}
