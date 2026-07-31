namespace Gaia.Modules.Inventory.Infrastructure;

public sealed class Product
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = "";
    public string ClassName { get; set; } = "";
    public string Subcategory { get; set; } = "";
    public string Name { get; set; } = "";
    public string ControlLevel { get; set; } = "Baja";
    public bool IsActive { get; set; } = true;
}

public sealed class Brand
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public bool IsActive { get; set; } = true;
}

public sealed class InventoryItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string AssetCode { get; set; } = "";
    public Guid ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public Guid? BrandId { get; set; }
    public Brand? Brand { get; set; }
    public string? Model { get; set; }
    public string? SerialNumber { get; set; }
    public DateOnly? IntakeDate { get; set; }
    public string Condition { get; set; } = "Sin evaluar";
    public string Status { get; set; } = "Disponible";
    public string? CostCenter { get; set; }
    public string? Funder { get; set; }
    public decimal? Value { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class InventoryAssignment
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InventoryItemId { get; set; }
    public InventoryItem InventoryItem { get; set; } = null!;
    public Guid? ThirdPartyId { get; set; }
    public string? ThirdPartyDocument { get; set; }
    public string CustodianName { get; set; } = "";
    public string? OrganizationalUnitCode { get; set; }
    public string? ActNumber { get; set; }
    public DateOnly AssignedOn { get; set; }
    public DateOnly? ReturnedOn { get; set; }
    public bool IsActive { get; set; }
}

public sealed class InventoryMovement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InventoryItemId { get; set; }
    public InventoryItem InventoryItem { get; set; } = null!;
    public string Type { get; set; } = "";
    public DateTimeOffset OccurredAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public string Description { get; set; } = "";
    public string Actor { get; set; } = "";
}

public sealed class InventoryImportIssue
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string BatchId { get; set; } = "";
    public int SourceRow { get; set; }
    public string Severity { get; set; } = "warning";
    public string Code { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
