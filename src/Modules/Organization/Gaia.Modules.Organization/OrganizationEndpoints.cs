using System.Security.Claims;
using Gaia.Modules.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;

namespace Gaia.Modules.Organization;

internal static class OrganizationEndpoints
{
    public static void Map(IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/organization")
            .WithTags("Organization")
            .RequireAuthorization();

        group.MapGet("/unit-types", ListUnitTypesAsync).RequireAuthorization(AdminCorePermissions.OrgCatalogosVer);
        group.MapPost("/unit-types", CreateUnitTypeAsync)
            .RequireAuthorization(AdminCorePermissions.OrgCatalogosCrear);
        group.MapPut("/unit-types/{id:guid}", UpdateUnitTypeAsync)
            .RequireAuthorization(AdminCorePermissions.OrgCatalogosActualizar);

        group.MapGet("/sites", ListSitesAsync).RequireAuthorization(AdminCorePermissions.OrgCatalogosVer);
        group.MapPost("/sites", CreateSiteAsync)
            .RequireAuthorization(AdminCorePermissions.OrgCatalogosCrear);
        group.MapPut("/sites/{id:guid}", UpdateSiteAsync)
            .RequireAuthorization(AdminCorePermissions.OrgCatalogosActualizar);

        group.MapGet("/units", ListUnitsAsync).RequireAuthorization(AdminCorePermissions.OrgUnidadesVer);
        group.MapPost("/units", CreateUnitAsync)
            .RequireAuthorization(AdminCorePermissions.OrgUnidadesCrear);
        group.MapPut("/units/{id:guid}", UpdateUnitAsync)
            .RequireAuthorization(AdminCorePermissions.OrgUnidadesActualizar);

        group.MapGet("/positions", ListPositionsAsync).RequireAuthorization(AdminCorePermissions.OrgCargosVer);
        group.MapPost("/positions", CreatePositionAsync)
            .RequireAuthorization(AdminCorePermissions.OrgCargosCrear);
        group.MapPut("/positions/{id:guid}", UpdatePositionAsync)
            .RequireAuthorization(AdminCorePermissions.OrgCargosActualizar);
    }

    private static async Task<IResult> ListUnitTypesAsync(
        IOrganizationUnitTypeReader reader,
        CancellationToken cancellationToken) =>
        Results.Ok(await reader.ListAsync(cancellationToken));

    private static async Task<IResult> CreateUnitTypeAsync(
        [FromBody] UnitTypeRequest request,
        ClaimsPrincipal principal,
        IOrganizationUnitTypeCreator creator,
        CancellationToken cancellationToken)
    {
        var result = await creator.CreateAsync(
            new UnitTypeCreateCommand(
                NormalizeCode(request.Code),
                request.Name.Trim(),
                Clean(request.Description),
                request.ColorToken.Trim(),
                request.VisualOrder,
                request.IsActive,
                Actor(principal)),
            cancellationToken);
        if (result.IsDuplicate)
        {
            return Conflict("Ya existe un tipo de unidad con ese código.");
        }
        var item = result.Item
            ?? throw new InvalidOperationException("Dataverse no devolvió el tipo de unidad creado.");
        return Results.Created($"/api/organization/unit-types/{item.Id}", item);
    }

    private static async Task<IResult> UpdateUnitTypeAsync(
        Guid id,
        [FromBody] UnitTypeRequest request,
        IOrganizationUnitTypeUpdater updater,
        CancellationToken cancellationToken)
    {
        var result = await updater.UpdateAsync(
            id,
            new UnitTypeUpdateCommand(
                NormalizeCode(request.Code),
                request.Name.Trim(),
                Clean(request.Description),
                request.ColorToken.Trim(),
                request.VisualOrder,
                request.IsActive),
            cancellationToken);
        if (result.NotFound)
        {
            return Results.NotFound();
        }
        var item = result.Item
            ?? throw new InvalidOperationException("Dataverse no devolvió el tipo actualizado.");
        return Results.Ok(item);
    }

    private static async Task<IResult> ListSitesAsync(
        IOrganizationSiteReader reader,
        CancellationToken cancellationToken) =>
        Results.Ok(await reader.ListAsync(cancellationToken));

    private static async Task<IResult> CreateSiteAsync(
        [FromBody] SiteRequest request,
        ClaimsPrincipal principal,
        IOrganizationSiteCreator creator,
        CancellationToken cancellationToken)
    {
        var result = await creator.CreateAsync(new SiteCreateCommand(
            NormalizeCode(request.Code),
            request.Name.Trim(),
            Clean(request.City),
            Clean(request.Address),
            request.IsActive,
            Actor(principal)), cancellationToken);
        if (result.IsDuplicate)
        {
            return Conflict("Ya existe una sede con ese código.");
        }
        var item = result.Item
            ?? throw new InvalidOperationException("Dataverse no devolvió la sede creada.");
        return Results.Created($"/api/organization/sites/{item.Id}", item);
    }

    private static async Task<IResult> UpdateSiteAsync(
        Guid id,
        [FromBody] SiteRequest request,
        IOrganizationSiteUpdater updater,
        CancellationToken cancellationToken)
    {
        var result = await updater.UpdateAsync(id, new SiteUpdateCommand(
            NormalizeCode(request.Code),
            request.Name.Trim(),
            Clean(request.City),
            Clean(request.Address),
            request.IsActive), cancellationToken);
        if (result.NotFound)
        {
            return Results.NotFound();
        }
        var item = result.Item
            ?? throw new InvalidOperationException("Dataverse no devolvió la sede actualizada.");
        return Results.Ok(item);
    }

    private static async Task<IResult> ListUnitsAsync(
        [AsParameters] UnitFilters filters,
        IOrganizationUnitReader reader,
        CancellationToken cancellationToken) =>
        Results.Ok(await reader.ListAsync(filters, cancellationToken));

    private static async Task<IResult> CreateUnitAsync(
        [FromBody] UnitRequest request,
        ClaimsPrincipal principal,
        IOrganizationUnitCreator creator,
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

        var result = await creator.CreateAsync(new OrganizationUnitCreateCommand(
            code,
            request.Name.Trim(),
            Clean(request.ShortName),
            request.UnitTypeId,
            request.ParentId,
            request.SiteId,
            Clean(request.Description),
            request.VisualOrder,
            request.EffectiveFrom,
            request.EffectiveTo,
            request.IsActive,
            Actor(principal)), cancellationToken);

        return result.Status switch
        {
            OrganizationUnitCreateStatus.DuplicateCode => Conflict("Ya existe una unidad con ese código."),
            OrganizationUnitCreateStatus.InvalidUnitType => Validation("El tipo de unidad no existe o está inactivo."),
            OrganizationUnitCreateStatus.ParentNotFound => Validation("La unidad padre no existe."),
            OrganizationUnitCreateStatus.SiteNotFound => Validation("La sede indicada no existe."),
            OrganizationUnitCreateStatus.Created when result.Id.HasValue =>
                Results.Created($"/api/organization/units/{result.Id.Value}", new { Id = result.Id.Value }),
            _ => throw new InvalidOperationException("Dataverse no devolvió el identificador de la unidad creada.")
        };
    }

    private static async Task<IResult> UpdateUnitAsync(
        Guid id,
        [FromBody] UnitRequest request,
        ClaimsPrincipal principal,
        IOrganizationUnitUpdater updater,
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

        var result = await updater.UpdateAsync(id, new OrganizationUnitUpdateCommand(
            code,
            request.Name.Trim(),
            Clean(request.ShortName),
            request.UnitTypeId,
            request.ParentId,
            request.SiteId,
            Clean(request.Description),
            request.VisualOrder,
            request.EffectiveFrom,
            request.EffectiveTo,
            request.IsActive,
            Actor(principal)), cancellationToken);

        return result.Status switch
        {
            OrganizationUnitUpdateStatus.NotFound => Results.NotFound(),
            OrganizationUnitUpdateStatus.DuplicateCode => Conflict("Ya existe una unidad con ese código."),
            OrganizationUnitUpdateStatus.InvalidUnitType => Validation("El tipo de unidad no existe o está inactivo."),
            OrganizationUnitUpdateStatus.ParentNotFound => Validation("La unidad padre no existe."),
            OrganizationUnitUpdateStatus.SelfParent => Validation("Una unidad no puede ser su propio padre."),
            OrganizationUnitUpdateStatus.HierarchyCycle => Validation("El cambio produciría un ciclo en la jerarquía."),
            OrganizationUnitUpdateStatus.SiteNotFound => Validation("La sede indicada no existe."),
            OrganizationUnitUpdateStatus.Updated when result.Id.HasValue => Results.Ok(new { Id = result.Id.Value }),
            _ => throw new InvalidOperationException("Dataverse no confirmó la actualización de la unidad.")
        };
    }

    private static async Task<IResult> ListPositionsAsync(
        IOrganizationPositionStore store,
        CancellationToken cancellationToken) =>
        Results.Ok(await store.ListAsync(cancellationToken));

    private static async Task<IResult> CreatePositionAsync(
        [FromBody] PositionRequest request,
        ClaimsPrincipal principal,
        IOrganizationPositionStore store,
        CancellationToken cancellationToken)
    {
        var result = await store.CreateAsync(new(NormalizeOptionalCode(request.Code), request.Name.Trim(),
            Clean(request.Description), request.IsActive, Actor(principal)), cancellationToken);
        if (result.Status == PositionWriteStatus.DuplicateCode) return Conflict("Ya existe un cargo con ese código.");
        var item = result.Item ?? throw new InvalidOperationException("Dataverse no devolvió el cargo creado.");
        return Results.Created($"/api/organization/positions/{item.Id}", item);
    }

    private static async Task<IResult> UpdatePositionAsync(
        Guid id,
        [FromBody] PositionRequest request,
        ClaimsPrincipal principal,
        IOrganizationPositionStore store,
        CancellationToken cancellationToken)
    {
        var result = await store.UpdateAsync(id, new(NormalizeOptionalCode(request.Code), request.Name.Trim(),
            Clean(request.Description), request.IsActive, Actor(principal)), cancellationToken);
        if (result.Status == PositionWriteStatus.NotFound) return Results.NotFound();
        if (result.Status == PositionWriteStatus.DuplicateCode) return Conflict("Ya existe un cargo con ese código.");
        return Results.Ok(result.Item ?? throw new InvalidOperationException("Dataverse no devolvió el cargo actualizado."));
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
    private static string? NormalizeOptionalCode(string? code) =>
        string.IsNullOrWhiteSpace(code) ? null : code.Trim().ToUpperInvariant();

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
    string? Code,
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
