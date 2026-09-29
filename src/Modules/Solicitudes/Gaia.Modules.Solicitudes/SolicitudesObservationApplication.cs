namespace Gaia.Modules.Solicitudes;

public sealed record AttendSolicitudesObservation(
    Guid RequestId,
    Guid TransitionId,
    Guid ActorThirdPartyId,
    string Comment,
    string? OriginalName,
    string? ContentType,
    long Length);

public sealed record SolicitudesObservationResult(
    SolicitudesRequestDetail Request,
    SolicitudesAttachment? Attachment);

public interface ISolicitudesObservationApplication
{
    Task<SolicitudesObservationResult> AttendAsync(
        AttendSolicitudesObservation request,
        Stream content,
        CancellationToken cancellationToken);
}

public sealed class SolicitudesObservationApplication(
    ISolicitudesAttachmentApplication attachments,
    ISolicitudesConversationApplication conversation) : ISolicitudesObservationApplication
{
    public async Task<SolicitudesObservationResult> AttendAsync(
        AttendSolicitudesObservation request,
        Stream content,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.RequestId == Guid.Empty || request.TransitionId == Guid.Empty
            || request.ActorThirdPartyId == Guid.Empty)
            throw new ArgumentException("Los identificadores de la respuesta no son válidos.");
        var comment = request.Comment?.Trim();
        if (string.IsNullOrWhiteSpace(comment) || comment.Length is < 2 or > 4000)
            throw new ArgumentException("La respuesta debe tener entre 2 y 4000 caracteres.");
        var hasDocument = request.Length > 0 && !string.IsNullOrWhiteSpace(request.OriginalName);

        SolicitudesAttachment? uploaded = null;
        try
        {
            if (hasDocument)
            {
                uploaded = await attachments.UploadAsync(new(
                    request.RequestId,
                    null,
                    null,
                    request.ActorThirdPartyId,
                    AttachmentVisibility.Requester,
                    request.OriginalName!,
                    string.IsNullOrWhiteSpace(request.ContentType) ? "application/octet-stream" : request.ContentType,
                    request.Length), content, cancellationToken);
            }
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
