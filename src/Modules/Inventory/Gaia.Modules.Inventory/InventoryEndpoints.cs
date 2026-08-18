using System.Security.Claims;
using Gaia.Modules.Inventory.Infrastructure;
using Gaia.Modules.ThirdParties.Infrastructure;
using Gaia.Modules.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Gaia.Modules.Inventory;

internal static class InventoryEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/inventory").WithTags("Inventory").RequireAuthorization();
        group.MapGet("/dashboard", DashboardAsync).RequireAuthorization(AdminCorePermissions.InvVer);
        group.MapGet("/products", ProductsAsync).RequireAuthorization(AdminCorePermissions.InvVer);
        group.MapGet("/items", ItemsAsync).RequireAuthorization(AdminCorePermissions.InvVer);
        group.MapGet("/items/{id:guid}", ItemAsync).RequireAuthorization(AdminCorePermissions.InvVer);
        group.MapGet("/assignments", AssignmentsAsync).RequireAuthorization(AdminCorePermissions.InvVer);
        group.MapGet("/movements", MovementsAsync).RequireAuthorization(AdminCorePermissions.InvVer);
        group.MapGet("/import-issues", IssuesAsync).RequireAuthorization(AdminCorePermissions.InvImportar);
        group.MapPost("/items/{id:guid}/assign", AssignAsync).RequireAuthorization(AdminCorePermissions.InvAsignar);
        group.MapPost("/import", ImportAsync).RequireAuthorization(AdminCorePermissions.InvImportar);
    }

    private static async Task<IResult> DashboardAsync(InventoryDbContext db, CancellationToken ct) => Results.Ok(new
    {
        products = await db.Products.CountAsync(ct), items = await db.Items.CountAsync(ct),
        available = await db.Items.CountAsync(x => x.Status == "Disponible", ct),
        assigned = await db.Assignments.CountAsync(x => x.IsActive, ct),
        issues = await db.ImportIssues.CountAsync(ct)
    });

    private static async Task<IResult> ProductsAsync(InventoryDbContext db, CancellationToken ct) => Results.Ok(await db.Products.AsNoTracking().OrderBy(x => x.ClassName).ThenBy(x => x.Name).ToListAsync(ct));
    private static async Task<IResult> ItemsAsync(string? search, string? status, InventoryDbContext db, CancellationToken ct)
    {
        var query = db.Items.AsNoTracking().Include(x => x.Product).Include(x => x.Brand).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search)) { var p = $"%{search.Trim()}%"; query = query.Where(x => EF.Functions.ILike(x.AssetCode, p) || EF.Functions.ILike(x.Product.Name, p) || (x.SerialNumber != null && EF.Functions.ILike(x.SerialNumber, p))); }
        if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
        return Results.Ok(await query.OrderByDescending(x => x.AssetCode).Select(x => new { x.Id, x.AssetCode, product = x.Product.Name, productCode = x.Product.Code, brand = x.Brand == null ? null : x.Brand.Name, x.Model, x.SerialNumber, x.Condition, x.Status, x.CostCenter, x.Funder, x.Value }).ToListAsync(ct));
    }

    private static async Task<IResult> ItemAsync(Guid id, InventoryDbContext db, CancellationToken ct)
    {
        var item = await db.Items.AsNoTracking().Include(x => x.Product).Include(x => x.Brand).FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) return Results.NotFound();
        return Results.Ok(new { item, assignments = await db.Assignments.AsNoTracking().Where(x => x.InventoryItemId == id).OrderByDescending(x => x.AssignedOn).ToListAsync(ct), movements = await db.Movements.AsNoTracking().Where(x => x.InventoryItemId == id).OrderByDescending(x => x.OccurredAtUtc).ToListAsync(ct) });
    }

    private static async Task<IResult> AssignmentsAsync(InventoryDbContext db, CancellationToken ct) => Results.Ok(await db.Assignments.AsNoTracking().Include(x => x.InventoryItem).ThenInclude(x => x.Product).OrderByDescending(x => x.AssignedOn).Select(x => new { x.Id, x.InventoryItemId, x.InventoryItem.AssetCode, product = x.InventoryItem.Product.Name, x.CustodianName, x.ThirdPartyDocument, x.OrganizationalUnitCode, x.ActNumber, x.AssignedOn, x.ReturnedOn, x.IsActive }).ToListAsync(ct));
    private static async Task<IResult> MovementsAsync(InventoryDbContext db, CancellationToken ct) => Results.Ok(await db.Movements.AsNoTracking().OrderByDescending(x => x.OccurredAtUtc).Take(250).ToListAsync(ct));
    private static async Task<IResult> IssuesAsync(InventoryDbContext db, CancellationToken ct) => Results.Ok(await db.ImportIssues.AsNoTracking().OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.SourceRow).ToListAsync(ct));

    private static async Task<IResult> AssignAsync(Guid id, AssignmentRequest request, ClaimsPrincipal principal, InventoryDbContext db, CancellationToken ct)
    {
        var item = await db.Items.FindAsync([id], ct); if (item is null) return Results.NotFound();
        var current = await db.Assignments.FirstOrDefaultAsync(x => x.InventoryItemId == id && x.IsActive, ct);
        if (current is not null) return Results.Conflict(new { message = "El elemento ya tiene una asignación activa." });
        var assignment = new InventoryAssignment { InventoryItemId = id, ThirdPartyId = request.ThirdPartyId, ThirdPartyDocument = request.ThirdPartyDocument, CustodianName = request.CustodianName.Trim(), OrganizationalUnitCode = request.OrganizationalUnitCode, ActNumber = request.ActNumber, AssignedOn = request.AssignedOn, IsActive = true };
        db.Assignments.Add(assignment); item.Status = "Asignado";
        db.Movements.Add(new InventoryMovement { InventoryItemId = id, Type = "Asignación", Description = $"Asignado a {assignment.CustodianName}", Actor = Actor(principal) });
        await db.SaveChangesAsync(ct); return Results.Created($"/api/inventory/items/{id}", new { assignment.Id });
    }

    private static async Task<IResult> ImportAsync(InventoryImportRequest request, ClaimsPrincipal principal, InventoryDbContext db, ThirdPartiesDbContext parties, CancellationToken ct)
    {
        var batch = $"INV-{DateTime.UtcNow:yyyyMMddHHmmss}"; var productCount = 0; var brandCount = 0; var itemCount = 0; var assignmentCount = 0; var warnings = 0;
        foreach (var row in request.Products) { var product = await db.Products.FirstOrDefaultAsync(x => x.Code == row.Code, ct); if (product is null) { product = new Product { Code = row.Code, ClassName = row.ClassName, Subcategory = row.Subcategory, Name = row.Name, ControlLevel = row.ControlLevel, IsActive = !string.Equals(row.State, "Inactivo", StringComparison.OrdinalIgnoreCase) }; db.Products.Add(product); productCount++; } }
        foreach (var name in request.Brands.Select(x => x.Trim().ToUpperInvariant()).Where(x => x.Length > 0).Distinct()) if (!await db.Brands.AnyAsync(x => x.Name == name, ct)) { db.Brands.Add(new Brand { Name = name }); brandCount++; }
        await db.SaveChangesAsync(ct);
        var products = await db.Products.ToDictionaryAsync(x => x.Code, ct); var brands = await db.Brands.ToDictionaryAsync(x => x.Name, ct);
        foreach (var row in request.Items)
        {
            if (!products.TryGetValue(row.ProductCode, out var product)) { db.ImportIssues.Add(Issue(batch, row.SourceRow, "PRODUCT_NOT_FOUND", $"Producto {row.ProductCode} no resuelto.")); warnings++; continue; }
            var code = row.AssetCode.Trim(); if (string.IsNullOrEmpty(code)) continue;
            var item = await db.Items.FirstOrDefaultAsync(x => x.AssetCode == code, ct);
            brands.TryGetValue(row.Brand.Trim().ToUpperInvariant(), out var brand);
            if (item is null) { item = new InventoryItem { AssetCode = code, ProductId = product.Id, BrandId = brand?.Id, Model = Clean(row.Model), SerialNumber = Clean(row.SerialNumber), IntakeDate = row.IntakeDate, Condition = Clean(row.Condition) ?? "Sin evaluar", Status = Clean(row.Status) ?? "Disponible", CostCenter = Clean(row.CostCenter), Funder = Clean(row.Funder), Value = row.Value, Notes = Clean(row.Notes) }; db.Items.Add(item); itemCount++; }
        }
        await db.SaveChangesAsync(ct);
        var items = await db.Items.ToDictionaryAsync(x => x.AssetCode, ct); var partyMap = await parties.ThirdParties.AsNoTracking().ToDictionaryAsync(x => x.DocumentNumber, ct);
        var lastAssignmentRows = request.Assignments
            .GroupBy(row => row.AssetCode.Trim())
            .ToDictionary(group => group.Key, group => group.Max(row => row.SourceRow));
        foreach (var row in request.Assignments)
        {
            if (!items.TryGetValue(row.AssetCode, out var item)) { db.ImportIssues.Add(Issue(batch, row.SourceRow, "ITEM_NOT_FOUND", $"Elemento {row.AssetCode} no resuelto.")); warnings++; continue; }
            var isLatest = lastAssignmentRows[row.AssetCode.Trim()] == row.SourceRow;
            if (isLatest && await db.Assignments.AnyAsync(x => x.InventoryItemId == item.Id && x.IsActive, ct)) continue;
            partyMap.TryGetValue(row.DocumentNumber.Trim(), out var party);
            if (party is null) { db.ImportIssues.Add(Issue(batch, row.SourceRow, "CUSTODIAN_NOT_FOUND", $"Tercero {row.DocumentNumber} no resuelto para {row.CustodianName}.")); warnings++; }
            db.Assignments.Add(new InventoryAssignment { InventoryItemId = item.Id, ThirdPartyId = party?.Id, ThirdPartyDocument = Clean(row.DocumentNumber), CustodianName = row.CustodianName.Trim(), OrganizationalUnitCode = Clean(row.AreaCode), ActNumber = Clean(row.ActNumber), AssignedOn = row.AssignedOn ?? DateOnly.FromDateTime(DateTime.Today), IsActive = isLatest });
            if (isLatest) item.Status = "Asignado";
            db.Movements.Add(new InventoryMovement { InventoryItemId = item.Id, Type = "Importación de asignación", Description = $"Asignado a {row.CustodianName}", Actor = Actor(principal) }); assignmentCount++;
        }
        await db.SaveChangesAsync(ct); return Results.Ok(new { batchId = batch, products = productCount, brands = brandCount, items = itemCount, assignments = assignmentCount, warnings });
    }

    private static InventoryImportIssue Issue(string batch, int row, string code, string message) => new() { BatchId = batch, SourceRow = row, Code = code, Message = message };
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) || value.Equals("N/A", StringComparison.OrdinalIgnoreCase) || value.Equals("#N/A", StringComparison.OrdinalIgnoreCase) ? null : value.Trim();
    private static string Actor(ClaimsPrincipal principal) => principal.FindFirstValue(ClaimTypes.Email) ?? principal.Identity?.Name ?? "system";
}

public sealed record AssignmentRequest(Guid? ThirdPartyId, string? ThirdPartyDocument, string CustodianName, string? OrganizationalUnitCode, string? ActNumber, DateOnly AssignedOn);
public sealed record InventoryImportRequest(IReadOnlyList<ProductImportRow> Products, IReadOnlyList<string> Brands, IReadOnlyList<ItemImportRow> Items, IReadOnlyList<AssignmentImportRow> Assignments);
public sealed record ProductImportRow(int SourceRow, string Code, string ClassName, string Subcategory, string Name, string ControlLevel, string? State);
public sealed record ItemImportRow(int SourceRow, string AssetCode, DateOnly? IntakeDate, string ProductCode, string Brand, string? Model, string? SerialNumber, string? Condition, string? Notes, string? CostCenter, string? Funder, decimal? Value, string? Status);
public sealed record AssignmentImportRow(int SourceRow, string AssetCode, string? ActNumber, DateOnly? AssignedOn, string DocumentNumber, string CustodianName, string? AreaCode);
