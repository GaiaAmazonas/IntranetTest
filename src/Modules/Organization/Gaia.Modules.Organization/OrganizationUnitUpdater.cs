namespace Gaia.Modules.Organization;

public interface IOrganizationUnitUpdater
{
    Task<OrganizationUnitUpdateResult> UpdateAsync(
        Guid id,
        OrganizationUnitUpdateCommand command,
        CancellationToken cancellationToken);
}

public sealed record OrganizationUnitUpdateCommand(
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
    string UpdatedBy);

public enum OrganizationUnitUpdateStatus
{
    Updated,
    NotFound,
    DuplicateCode,
    InvalidUnitType,
    ParentNotFound,
    SelfParent,
    HierarchyCycle,
    SiteNotFound
}

public sealed record OrganizationUnitUpdateResult(
    OrganizationUnitUpdateStatus Status,
    Guid? Id = null);
