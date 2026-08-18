namespace Gaia.Modules.Organization;

public interface IOrganizationUnitReader
{
    Task<IReadOnlyList<UnitResponse>> ListAsync(
        UnitFilters filters,
        CancellationToken cancellationToken);
}
