namespace Gaia.Modules.Solicitudes;

public sealed record SolicitudesPortalArea(Guid Id, string Name, string ShortName, int Order);

public sealed record SolicitudesPortalService(Guid Id, Guid AreaId, string Code, string Name, string? Description,
    string? Instructions, bool AllowsAttachments, int MaximumAttachments, int MaximumFileMb, int Order);

public sealed record SolicitudesPortalRequest(Guid Id, string Number, string Subject, string Service,
    string Status, string? StatusColor, bool IsFinal, DateTimeOffset? SubmittedAt, DateOnly? DueDate);

public sealed record SolicitudesPortalSnapshot(int Open, int InProgress, int ResolvedRecently,
    IReadOnlyList<SolicitudesPortalArea> Areas, IReadOnlyList<SolicitudesPortalService> Services,
    IReadOnlyList<SolicitudesPortalRequest> Requests);

public sealed record SolicitudesPortalCatalog(IReadOnlyList<SolicitudesPortalArea> Areas,
    IReadOnlyList<SolicitudesPortalService> Services);

public interface ISolicitudesPortalReader
{
    Task<SolicitudesPortalCatalog> ReadCatalogAsync(CancellationToken cancellationToken);
    Task<SolicitudesPortalSnapshot> ReadAsync(Guid actorThirdPartyId, CancellationToken cancellationToken);
}
