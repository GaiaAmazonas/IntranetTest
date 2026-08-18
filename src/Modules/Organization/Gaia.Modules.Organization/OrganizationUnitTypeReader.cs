namespace Gaia.Modules.Organization;

public interface IOrganizationUnitTypeReader
{
    Task<IReadOnlyList<UnitTypeResponse>> ListAsync(CancellationToken cancellationToken);
}

public sealed record UnitTypeResponse(
    Guid Id,
    DateTimeOffset CreatedAtUtc,
    string CreatedBy,
    DateTimeOffset? UpdatedAtUtc,
    string? UpdatedBy,
    string Code,
    string Name,
    string? Description,
    string ColorToken,
    int VisualOrder,
    bool IsActive);
