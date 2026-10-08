namespace Gaia.Modules.Solicitudes;

public sealed record SolicitudesComment(Guid Id, string Content, DateTimeOffset PublishedAt, bool IsInternal,
    bool IsMine, string AuthorRole);
public sealed record SolicitudesTransition(Guid Id, Guid TargetStateId, string TargetState, bool RequiresComment,
    bool RequiresReason, bool RequiresSolution, bool RequestsRating, bool IsObservationReturn = false);
public sealed record SolicitudesHistoryEvent(Guid Id,string Title,string? Detail,DateTimeOffset OccurredAt,
    string Actor,int Movement,int Origin,bool VisibleToRequester);
public sealed record SolicitudesRequestDetail(Guid Id, string Number, string Subject, string Description,
    string Service, string Status, string? StatusColor, DateTimeOffset? SubmittedAt, DateOnly? DueDate,
    bool AllowsRequesterComments, bool IsManager, IReadOnlyList<SolicitudesComment> Comments,
    IReadOnlyList<SolicitudesTransition> Transitions,IReadOnlyList<SolicitudesHistoryEvent>? History=null,
    IReadOnlyList<SolicitudesWorkflowAnswerItem>? FormAnswers=null);
public sealed record AddSolicitudesComment(string Content, bool Internal = false);
public sealed record ApplySolicitudesTransition(Guid TransitionId, string? Comment, string? Reason, string? Solution,
    int? Rating=null,string? RatingComment=null);

public interface ISolicitudesConversationStore
{
    Task<SolicitudesRequestDetail?> ReadAsync(Guid requestId, Guid actorThirdPartyId, bool managementAccess, CancellationToken cancellationToken);
    Task<SolicitudesComment> AddAsync(Guid requestId, Guid actorThirdPartyId, string content,
        bool internalOnly, bool managementAccess, DateTimeOffset now, CancellationToken cancellationToken);
    Task<SolicitudesRequestDetail> TransitionAsync(Guid requestId, Guid actorThirdPartyId,
        ApplySolicitudesTransition transition, bool managementAccess, DateTimeOffset now, CancellationToken cancellationToken);
}

public interface ISolicitudesConversationApplication
{
    Task<SolicitudesRequestDetail> ReadAsync(Guid requestId, Guid actorThirdPartyId, bool managementAccess, CancellationToken cancellationToken);
    Task<SolicitudesComment> AddAsync(Guid requestId, Guid actorThirdPartyId, AddSolicitudesComment request,
        bool managementAccess, CancellationToken cancellationToken);
    Task<SolicitudesRequestDetail> TransitionAsync(Guid requestId, Guid actorThirdPartyId,
        ApplySolicitudesTransition request, bool managementAccess, CancellationToken cancellationToken);
}

public sealed class SolicitudesConversationApplication(ISolicitudesConversationStore store, TimeProvider timeProvider)
    : ISolicitudesConversationApplication
{
    public async Task<SolicitudesRequestDetail> ReadAsync(Guid requestId, Guid actorThirdPartyId, bool managementAccess, CancellationToken cancellationToken) =>
        await store.ReadAsync(requestId, actorThirdPartyId, managementAccess, cancellationToken)
        ?? throw new KeyNotFoundException("La solicitud no existe o no tienes acceso.");

    public Task<SolicitudesComment> AddAsync(Guid requestId, Guid actorThirdPartyId, AddSolicitudesComment request,
        bool managementAccess, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var content = request.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content) || content.Length is < 2 or > 4000)
            throw new ArgumentException("El comentario debe tener entre 2 y 4000 caracteres.");
        return store.AddAsync(requestId, actorThirdPartyId, content, request.Internal, managementAccess, timeProvider.GetUtcNow(), cancellationToken);
    }

    public Task<SolicitudesRequestDetail> TransitionAsync(Guid requestId, Guid actorThirdPartyId,
        ApplySolicitudesTransition request, bool managementAccess, CancellationToken cancellationToken) =>
        store.TransitionAsync(requestId, actorThirdPartyId, request, managementAccess, timeProvider.GetUtcNow(), cancellationToken);
}
