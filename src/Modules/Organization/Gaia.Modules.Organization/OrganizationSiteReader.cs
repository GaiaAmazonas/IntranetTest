namespace Gaia.Modules.Organization;

public interface IOrganizationSiteReader
{
    Task<IReadOnlyList<SiteResponse>> ListAsync(CancellationToken cancellationToken);
}

public sealed record SiteResponse(
    Guid Id,
    DateTimeOffset CreatedAtUtc,
    string CreatedBy,
    DateTimeOffset? UpdatedAtUtc,
    string? UpdatedBy,
    string Code,
    string Name,
    string? City,
    string? Address,
    bool IsActive);
