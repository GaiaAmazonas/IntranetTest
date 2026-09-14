namespace Gaia.Modules.Helpdesk;

public sealed record HelpdeskComment(Guid Id, string Content, DateTimeOffset PublishedAt, bool IsInternal,
    bool IsMine, string AuthorRole);
public sealed record HelpdeskTransition(Guid Id, Guid TargetStateId, string TargetState, bool RequiresComment,
    bool RequiresReason, bool RequiresSolution, bool RequestsRating, bool IsObservationReturn = false);
public sealed record HelpdeskRequestDetail(Guid Id, string Number, string Subject, string Description,
    string Service, string Status, string? StatusColor, DateTimeOffset? SubmittedAt, DateOnly? DueDate,
    bool AllowsRequesterComments, bool IsManager, IReadOnlyList<HelpdeskComment> Comments,
    IReadOnlyList<HelpdeskTransition> Transitions);
public sealed record AddHelpdeskComment(string Content, bool Internal = false);
public sealed record ApplyHelpdeskTransition(Guid TransitionId, string? Comment, string? Reason, string? Solution,
    int? Rating=null,string? RatingComment=null);

public interface IHelpdeskConversationStore
{
    Task<HelpdeskRequestDetail?> ReadAsync(Guid requestId, Guid actorThirdPartyId, bool managementAccess, CancellationToken cancellationToken);
    Task<HelpdeskComment> AddAsync(Guid requestId, Guid actorThirdPartyId, string content,
        bool internalOnly, bool managementAccess, DateTimeOffset now, CancellationToken cancellationToken);
    Task<HelpdeskRequestDetail> TransitionAsync(Guid requestId, Guid actorThirdPartyId,
        ApplyHelpdeskTransition transition, bool managementAccess, DateTimeOffset now, CancellationToken cancellationToken);
}

public interface IHelpdeskConversationApplication
{
    Task<HelpdeskRequestDetail> ReadAsync(Guid requestId, Guid actorThirdPartyId, bool managementAccess, CancellationToken cancellationToken);
    Task<HelpdeskComment> AddAsync(Guid requestId, Guid actorThirdPartyId, AddHelpdeskComment request,
        bool managementAccess, CancellationToken cancellationToken);
    Task<HelpdeskRequestDetail> TransitionAsync(Guid requestId, Guid actorThirdPartyId,
        ApplyHelpdeskTransition request, bool managementAccess, CancellationToken cancellationToken);
}

public sealed class HelpdeskConversationApplication(IHelpdeskConversationStore store, TimeProvider timeProvider)
    : IHelpdeskConversationApplication
{
    public async Task<HelpdeskRequestDetail> ReadAsync(Guid requestId, Guid actorThirdPartyId, bool managementAccess, CancellationToken cancellationToken) =>
        await store.ReadAsync(requestId, actorThirdPartyId, managementAccess, cancellationToken)
        ?? throw new KeyNotFoundException("La solicitud no existe o no tienes acceso.");

    public Task<HelpdeskComment> AddAsync(Guid requestId, Guid actorThirdPartyId, AddHelpdeskComment request,
        bool managementAccess, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var content = request.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content) || content.Length is < 2 or > 4000)
            throw new ArgumentException("El comentario debe tener entre 2 y 4000 caracteres.");
        return store.AddAsync(requestId, actorThirdPartyId, content, request.Internal, managementAccess, timeProvider.GetUtcNow(), cancellationToken);
    }

    public Task<HelpdeskRequestDetail> TransitionAsync(Guid requestId, Guid actorThirdPartyId,
        ApplyHelpdeskTransition request, bool managementAccess, CancellationToken cancellationToken) =>
        store.TransitionAsync(requestId, actorThirdPartyId, request, managementAccess, timeProvider.GetUtcNow(), cancellationToken);
}
