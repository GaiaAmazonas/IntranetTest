namespace Gaia.Modules.Helpdesk;

public sealed record HelpdeskPortalService(Guid Id, string Code, string Name, string? Description,
    string? Instructions, bool AllowsAttachments, int MaximumAttachments, int MaximumFileMb, int Order);

public sealed record HelpdeskPortalRequest(Guid Id, string Number, string Subject, string Service,
    string Status, string? StatusColor, bool IsFinal, DateTimeOffset? SubmittedAt, DateOnly? DueDate);

public sealed record HelpdeskPortalSnapshot(int Open, int InProgress, int ResolvedRecently,
    IReadOnlyList<HelpdeskPortalService> Services, IReadOnlyList<HelpdeskPortalRequest> Requests);

public interface IHelpdeskPortalReader
{
    Task<HelpdeskPortalSnapshot> ReadAsync(Guid actorThirdPartyId, CancellationToken cancellationToken);
}
