namespace Gaia.Modules.Organization;

public interface IOrganizationUnitCreator
{
    Task<OrganizationUnitCreateResult> CreateAsync(
        OrganizationUnitCreateCommand command,
        CancellationToken cancellationToken);
}

public sealed record OrganizationUnitCreateCommand(
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
    bool IsActive,
    string CreatedBy);

public enum OrganizationUnitCreateStatus
{
    Created,
    DuplicateCode,
    InvalidUnitType,
    ParentNotFound,
    SiteNotFound
}

public sealed record OrganizationUnitCreateResult(
    OrganizationUnitCreateStatus Status,
    Guid? Id = null);
