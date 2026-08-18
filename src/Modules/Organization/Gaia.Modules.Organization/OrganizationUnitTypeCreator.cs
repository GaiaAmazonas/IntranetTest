namespace Gaia.Modules.Organization;

public interface IOrganizationUnitTypeCreator
{
    Task<UnitTypeCreateResult> CreateAsync(
        UnitTypeCreateCommand command,
        CancellationToken cancellationToken);
}

public sealed record UnitTypeCreateCommand(
    string Code,
    string Name,
    string? Description,
    string ColorToken,
    int VisualOrder,
    bool IsActive,
    string CreatedBy);

public sealed record UnitTypeCreateResult(bool IsDuplicate, UnitTypeResponse? Item);
