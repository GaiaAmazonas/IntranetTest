namespace Gaia.Modules.Helpdesk;

public sealed record AttendHelpdeskObservation(
    Guid RequestId,
    Guid TransitionId,
    Guid ActorThirdPartyId,
    string Comment,
    string OriginalName,
    string ContentType,
    long Length);

public sealed record HelpdeskObservationResult(
    HelpdeskRequestDetail Request,
    HelpdeskAttachment Attachment);

public interface IHelpdeskObservationApplication
{
    Task<HelpdeskObservationResult> AttendAsync(
        AttendHelpdeskObservation request,
        Stream content,
        CancellationToken cancellationToken);
}

public sealed class HelpdeskObservationApplication(
    IHelpdeskAttachmentApplication attachments,
    IHelpdeskConversationApplication conversation) : IHelpdeskObservationApplication
{
    public async Task<HelpdeskObservationResult> AttendAsync(
        AttendHelpdeskObservation request,
        Stream content,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(content);
        if (request.RequestId == Guid.Empty || request.TransitionId == Guid.Empty
            || request.ActorThirdPartyId == Guid.Empty)
            throw new ArgumentException("Los identificadores de la respuesta no son válidos.");
        var comment = request.Comment?.Trim();
        if (string.IsNullOrWhiteSpace(comment) || comment.Length is < 2 or > 4000)
            throw new ArgumentException("La respuesta debe tener entre 2 y 4000 caracteres.");
        if (request.Length <= 0 || string.IsNullOrWhiteSpace(request.OriginalName))
            throw new ArgumentException("Debes adjuntar un documento.");

        HelpdeskAttachment? uploaded = null;
        try
        {
            uploaded = await attachments.UploadAsync(new(
                request.RequestId,
                null,
                null,
                request.ActorThirdPartyId,
                AttachmentVisibility.Requester,
                request.OriginalName,
                request.ContentType,
                request.Length), content, cancellationToken);
            var updated = await conversation.TransitionAsync(
                request.RequestId,
                request.ActorThirdPartyId,
                new(request.TransitionId, comment, null, null),
                managementAccess: false,
                cancellationToken);
            return new(updated, uploaded);
        }
        catch
        {
            if (uploaded is not null)
                await attachments.RollbackUploadAsync(uploaded, request.ActorThirdPartyId, CancellationToken.None);
            throw;
        }
    }
}
