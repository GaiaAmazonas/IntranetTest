using System.Security.Claims;
using Gaia.Modules.Organization.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;

namespace Gaia.Modules.Organization;

internal static class OrganizationEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/organization")
            .WithTags("Organization")
            .RequireAuthorization(OrganizationPermissions.Read);

        group.MapGet("/unit-types", ListUnitTypesAsync);
        group.MapPost("/unit-types", CreateUnitTypeAsync)
            .RequireAuthorization(OrganizationPermissions.Manage);
        group.MapPut("/unit-types/{id:guid}", UpdateUnitTypeAsync)
            .RequireAuthorization(OrganizationPermissions.Manage);

        group.MapGet("/sites", ListSitesAsync);
        group.MapPost("/sites", CreateSiteAsync)
            .RequireAuthorization(OrganizationPermissions.Manage);
        group.MapPut("/sites/{id:guid}", UpdateSiteAsync)
            .RequireAuthorization(OrganizationPermissions.Manage);

        group.MapGet("/units", ListUnitsAsync);
        group.MapPost("/units", CreateUnitAsync)
            .RequireAuthorization(OrganizationPermissions.Manage);
        group.MapPut("/units/{id:guid}", UpdateUnitAsync)
            .RequireAuthorization(OrganizationPermissions.Manage);

        group.MapGet("/positions", ListPositionsAsync);
        group.MapPost("/positions", CreatePositionAsync)
            .RequireAuthorization(OrganizationPermissions.Manage);
        group.MapPut("/positions/{id:guid}", UpdatePositionAsync)
            .RequireAuthorization(OrganizationPermissions.Manage);
    }

    private static async Task<IResult> ListUnitTypesAsync(
        OrganizationDbContext context,
        CancellationToken cancellationToken) =>
        Results.Ok(await context.UnitTypes
            .AsNoTracking()
            .OrderBy(item => item.VisualOrder)
            .ThenBy(item => item.Name)
            .ToListAsync(cancellationToken));

    private static async Task<IResult> CreateUnitTypeAsync(
        [FromBody] UnitTypeRequest request,
        ClaimsPrincipal principal,
        OrganizationDbContext context,
        CancellationToken cancellationToken)
    {
        if (await context.UnitTypes.AnyAsync(
                item => item.Code == NormalizeCode(request.Code),
                cancellationToken))
        {
            return Conflict("Ya existe un tipo de unidad con ese código.");
        }

        var item = new UnitType
        {
            Code = NormalizeCode(request.Code),
            Name = request.Name.Trim(),
            Description = Clean(request.Description),
            ColorToken = request.ColorToken.Trim(),
            VisualOrder = request.VisualOrder,
            IsActive = request.IsActive,
            CreatedBy = Actor(principal)
        };
        context.UnitTypes.Add(item);
        await context.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/organization/unit-types/{item.Id}", item);
    }

    private static async Task<IResult> UpdateUnitTypeAsync(
        Guid id,
        [FromBody] UnitTypeRequest request,
        ClaimsPrincipal principal,
        OrganizationDbContext context,
        CancellationToken cancellationToken)
    {
        var item = await context.UnitTypes.FindAsync([id], cancellationToken);
        if (item is null)
        {
            return Results.NotFound();
        }

        item.Code = NormalizeCode(request.Code);
        item.Name = request.Name.Trim();
        item.Description = Clean(request.Description);
        item.ColorToken = request.ColorToken.Trim();
        item.VisualOrder = request.VisualOrder;
        item.IsActive = request.IsActive;
        Touch(item, principal);
        await context.SaveChangesAsync(cancellationToken);
        return Results.Ok(item);
    }

    private static async Task<IResult> ListSitesAsync(
        OrganizationDbContext context,
        CancellationToken cancellationToken) =>
        Results.Ok(await context.Sites
            .AsNoTracking()
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken));

    private static async Task<IResult> CreateSiteAsync(
        [FromBody] SiteRequest request,
        ClaimsPrincipal principal,
        OrganizationDbContext context,
        CancellationToken cancellationToken)
    {
        if (await context.Sites.AnyAsync(
                item => item.Code == NormalizeCode(request.Code),
                cancellationToken))
        {
            return Conflict("Ya existe una sede con ese código.");
        }

        var item = new Site
        {
            Code = NormalizeCode(request.Code),
            Name = request.Name.Trim(),
            City = Clean(request.City),
            Address = Clean(request.Address),
            IsActive = request.IsActive,
            CreatedBy = Actor(principal)
        };
        context.Sites.Add(item);
        await context.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/organization/sites/{item.Id}", item);
    }

    private static async Task<IResult> UpdateSiteAsync(
        Guid id,
        [FromBody] SiteRequest request,
        ClaimsPrincipal principal,
        OrganizationDbContext context,
        CancellationToken cancellationToken)
    {
        var item = await context.Sites.FindAsync([id], cancellationToken);
        if (item is null)
        {
            return Results.NotFound();
        }

        item.Code = NormalizeCode(request.Code);
        item.Name = request.Name.Trim();
        item.City = Clean(request.City);
        item.Address = Clean(request.Address);
        item.IsActive = request.IsActive;
        Touch(item, principal);
        await context.SaveChangesAsync(cancellationToken);
        return Results.Ok(item);
    }

    private static async Task<IResult> ListUnitsAsync(
        [AsParameters] UnitFilters filters,
        OrganizationDbContext context,
        CancellationToken cancellationToken)
    {
        var query = context.Units
            .AsNoTracking()
            .Include(item => item.UnitType)
            .Include(item => item.Site)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filters.Search))
        {
            var pattern = $"%{filters.Search.Trim()}%";
            query = query.Where(item =>
                EF.Functions.ILike(item.Code, pattern)
                || EF.Functions.ILike(item.Name, pattern));
        }

        if (filters.IsActive.HasValue)
        {
            query = query.Where(item => item.IsActive == filters.IsActive);
        }

        if (filters.UnitTypeId.HasValue)
        {
            query = query.Where(item => item.UnitTypeId == filters.UnitTypeId);
        }

        var units = await query
            .OrderBy(item => item.Level)
            .ThenBy(item => item.VisualOrder)
            .ThenBy(item => item.Name)
            .Select(item => new UnitResponse(
                item.Id,
                item.Code,
                item.Name,
                item.ShortName,
                item.UnitTypeId,
                item.UnitType!.Name,
                item.UnitType.ColorToken,
                item.ParentId,
                item.SiteId,
                item.Site != null ? item.Site.Name : null,
                item.Level,
                item.Description,
                item.VisualOrder,
                item.EffectiveFrom,
                item.EffectiveTo,
                item.IsActive))
            .ToListAsync(cancellationToken);

        return Results.Ok(units);
    }

    private static async Task<IResult> CreateUnitAsync(
        [FromBody] UnitRequest request,
        ClaimsPrincipal principal,
        OrganizationDbContext context,
        CancellationToken cancellationToken)
    {
        var validation = await ValidateUnitRequestAsync(
            request,
            null,
            context,
            cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        var level = await OrganizationHierarchy.CalculateLevelAsync(
            request.ParentId,
            context,
            cancellationToken);
        var item = new OrganizationalUnit
        {
            Code = NormalizeCode(request.Code),
            Name = request.Name.Trim(),
            ShortName = Clean(request.ShortName),
            UnitTypeId = request.UnitTypeId,
            ParentId = request.ParentId,
            SiteId = request.SiteId,
            Level = level,
            Description = Clean(request.Description),
            VisualOrder = request.VisualOrder,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            IsActive = request.IsActive,
            CreatedBy = Actor(principal)
        };
        context.Units.Add(item);
        await context.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/organization/units/{item.Id}", new { item.Id });
    }

    private static async Task<IResult> UpdateUnitAsync(
        Guid id,
        [FromBody] UnitRequest request,
        ClaimsPrincipal principal,
        OrganizationDbContext context,
        CancellationToken cancellationToken)
    {
        var item = await context.Units.FindAsync([id], cancellationToken);
        if (item is null)
        {
            return Results.NotFound();
        }

        var validation = await ValidateUnitRequestAsync(
            request,
            id,
            context,
            cancellationToken);
        if (validation is not null)
        {
            return validation;
        }

        var parentChanged = item.ParentId != request.ParentId;
        item.Code = NormalizeCode(request.Code);
        item.Name = request.Name.Trim();
        item.ShortName = Clean(request.ShortName);
        item.UnitTypeId = request.UnitTypeId;
        item.ParentId = request.ParentId;
        item.SiteId = request.SiteId;
        item.Level = await OrganizationHierarchy.CalculateLevelAsync(
            request.ParentId,
            context,
            cancellationToken);
        item.Description = Clean(request.Description);
        item.VisualOrder = request.VisualOrder;
        item.EffectiveFrom = request.EffectiveFrom;
        item.EffectiveTo = request.EffectiveTo;
        item.IsActive = request.IsActive;
        Touch(item, principal);

        if (parentChanged)
        {
            await OrganizationHierarchy.RecalculateDescendantLevelsAsync(
                item,
                context,
                cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
        return Results.Ok(new { item.Id });
    }

    private static async Task<IResult?> ValidateUnitRequestAsync(
        UnitRequest request,
        Guid? existingId,
        OrganizationDbContext context,
        CancellationToken cancellationToken)
    {
        var code = NormalizeCode(request.Code);
        if (string.IsNullOrWhiteSpace(code) || string.IsNullOrWhiteSpace(request.Name))
        {
            return Validation("Código y nombre son obligatorios.");
        }

        if (request.EffectiveTo < request.EffectiveFrom)
        {
            return Validation("La fecha final no puede ser anterior a la inicial.");
        }

        if (await context.Units.AnyAsync(
                item => item.Code == code && item.Id != existingId,
                cancellationToken))
        {
            return Conflict("Ya existe una unidad con ese código.");
        }

        if (!await context.UnitTypes.AnyAsync(
                item => item.Id == request.UnitTypeId && item.IsActive,
                cancellationToken))
        {
            return Validation("El tipo de unidad no existe o está inactivo.");
        }

        if (request.ParentId.HasValue)
        {
            if (request.ParentId == existingId)
            {
                return Validation("Una unidad no puede ser su propio padre.");
            }

            var parent = await context.Units
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == request.ParentId, cancellationToken);
            if (parent is null)
            {
                return Validation("La unidad padre no existe.");
            }

            if (existingId.HasValue && await OrganizationHierarchy.WouldCreateCycleAsync(
                    existingId.Value,
                    request.ParentId.Value,
                    context,
                    cancellationToken))
            {
                return Validation("El cambio produciría un ciclo en la jerarquía.");
            }
        }

        if (request.SiteId.HasValue && !await context.Sites.AnyAsync(
                item => item.Id == request.SiteId,
                cancellationToken))
        {
            return Validation("La sede indicada no existe.");
        }

        return null;
    }

    private static async Task<IResult> ListPositionsAsync(
        OrganizationDbContext context,
        CancellationToken cancellationToken) =>
        Results.Ok(await context.Positions
            .AsNoTracking()
            .OrderBy(item => item.Name)
            .ToListAsync(cancellationToken));

    private static async Task<IResult> CreatePositionAsync(
        [FromBody] PositionRequest request,
        ClaimsPrincipal principal,
        OrganizationDbContext context,
        CancellationToken cancellationToken)
    {
        if (await context.Positions.AnyAsync(
                item => item.Code == NormalizeCode(request.Code),
                cancellationToken))
        {
            return Conflict("Ya existe un cargo con ese código.");
        }

        var item = new Position
        {
            Code = NormalizeCode(request.Code),
            Name = request.Name.Trim(),
            Description = Clean(request.Description),
            IsActive = request.IsActive,
            CreatedBy = Actor(principal)
        };
        context.Positions.Add(item);
        await context.SaveChangesAsync(cancellationToken);
        return Results.Created($"/api/organization/positions/{item.Id}", item);
    }

    private static async Task<IResult> UpdatePositionAsync(
        Guid id,
        [FromBody] PositionRequest request,
        ClaimsPrincipal principal,
        OrganizationDbContext context,
        CancellationToken cancellationToken)
    {
        var item = await context.Positions.FindAsync([id], cancellationToken);
        if (item is null)
        {
            return Results.NotFound();
        }

        item.Code = NormalizeCode(request.Code);
        item.Name = request.Name.Trim();
        item.Description = Clean(request.Description);
        item.IsActive = request.IsActive;
        Touch(item, principal);
        await context.SaveChangesAsync(cancellationToken);
        return Results.Ok(item);
    }

    private static string Actor(ClaimsPrincipal principal) =>
        principal.FindFirstValue(ClaimTypes.Email)
        ?? principal.Identity?.Name
        ?? "unknown";

    private static void Touch(AuditedEntity entity, ClaimsPrincipal principal)
    {
        entity.UpdatedAtUtc = DateTimeOffset.UtcNow;
        entity.UpdatedBy = Actor(principal);
    }

    private static string NormalizeCode(string code) => code.Trim().ToUpperInvariant();

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IResult Conflict(string detail) =>
        Results.Problem(statusCode: StatusCodes.Status409Conflict, detail: detail);

    private static IResult Validation(string detail) =>
        Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["organization"] = [detail]
        });
}

public sealed record UnitTypeRequest(
    string Code,
    string Name,
    string? Description,
    string ColorToken,
    int VisualOrder,
    bool IsActive);

public sealed record SiteRequest(
    string Code,
    string Name,
    string? City,
    string? Address,
    bool IsActive);

public sealed record PositionRequest(
    string Code,
    string Name,
    string? Description,
    bool IsActive);

public sealed record UnitRequest(
    string Code,
    string Name,
    string? ShortName,
    Guid UnitTypeId,
    Guid? ParentId,
    Guid? SiteId,
    string? Description,
    int VisualOrder,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    bool IsActive);

public sealed record UnitFilters(
    string? Search,
    bool? IsActive,
    Guid? UnitTypeId);

public sealed record UnitResponse(
    Guid Id,
    string Code,
    string Name,
    string? ShortName,
    Guid UnitTypeId,
    string UnitTypeName,
    string ColorToken,
    Guid? ParentId,
    Guid? SiteId,
    string? SiteName,
    int Level,
    string? Description,
    int VisualOrder,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    bool IsActive);
