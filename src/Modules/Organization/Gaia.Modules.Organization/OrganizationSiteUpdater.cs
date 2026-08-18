namespace Gaia.Modules.Organization;

public interface IOrganizationSiteUpdater
{
    Task<SiteUpdateResult> UpdateAsync(Guid id, SiteUpdateCommand command, CancellationToken cancellationToken);
}

public sealed record SiteUpdateCommand(
    string Code,
    string Name,
    string? City,
    string? Address,
    bool IsActive);

public sealed record SiteUpdateResult(bool NotFound, SiteResponse? Item);
