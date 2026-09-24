namespace Gaia.Modules.Helpdesk;

public sealed record HelpdeskPortalArea(Guid Id, string Name, string ShortName, int Order);

public sealed record HelpdeskPortalService(Guid Id, Guid AreaId, string Code, string Name, string? Description,
    string? Instructions, bool AllowsAttachments, int MaximumAttachments, int MaximumFileMb, int Order);

public sealed record HelpdeskPortalRequest(Guid Id, string Number, string Subject, string Service,
    string Status, string? StatusColor, bool IsFinal, DateTimeOffset? SubmittedAt, DateOnly? DueDate);

public sealed record HelpdeskPortalSnapshot(int Open, int InProgress, int ResolvedRecently,
    IReadOnlyList<HelpdeskPortalArea> Areas, IReadOnlyList<HelpdeskPortalService> Services,
    IReadOnlyList<HelpdeskPortalRequest> Requests);

public sealed record HelpdeskPortalCatalog(IReadOnlyList<HelpdeskPortalArea> Areas,
    IReadOnlyList<HelpdeskPortalService> Services);

public interface IHelpdeskPortalReader
{
    Task<HelpdeskPortalCatalog> ReadCatalogAsync(CancellationToken cancellationToken);
    Task<HelpdeskPortalSnapshot> ReadAsync(Guid actorThirdPartyId, CancellationToken cancellationToken);
}
