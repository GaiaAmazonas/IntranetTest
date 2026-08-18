namespace Gaia.Modules.Organization;

public interface IOrganizationUnitTypeUpdater
{
    Task<UnitTypeUpdateResult> UpdateAsync(
        Guid id,
        UnitTypeUpdateCommand command,
        CancellationToken cancellationToken);
}

public sealed record UnitTypeUpdateCommand(
    string Code,
    string Name,
    string? Description,
    string ColorToken,
    int VisualOrder,
    bool IsActive);

public sealed record UnitTypeUpdateResult(bool NotFound, UnitTypeResponse? Item);
