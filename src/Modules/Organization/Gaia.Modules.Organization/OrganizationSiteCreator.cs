namespace Gaia.Modules.Organization;

public interface IOrganizationSiteCreator
{
    Task<SiteCreateResult> CreateAsync(SiteCreateCommand command, CancellationToken cancellationToken);
}

public sealed record SiteCreateCommand(
    string Code,
    string Name,
    string? City,
    string? Address,
    bool IsActive,
    string CreatedBy);

public sealed record SiteCreateResult(bool IsDuplicate, SiteResponse? Item);
