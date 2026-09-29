using Gaia.BuildingBlocks.Files;

namespace Gaia.Modules.Solicitudes;

public enum AttachmentVisibility { Requester = 299540080, Internal = 299540081 }

public static class SolicitudesAttachmentRules
{
    public const int SharePointProvider = 299540090;

    public static void Validate(PersistSolicitudesAttachment value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Id == Guid.Empty || value.RequestId == Guid.Empty || value.UploadedByThirdPartyId == Guid.Empty)
            throw new ArgumentException("Los identificadores obligatorios del adjunto no son válidos.");
        if (value.Visibility is not (AttachmentVisibility.Requester or AttachmentVisibility.Internal))
            throw new ArgumentException("La visibilidad del adjunto no es válida.");
        if (value.File.Id.Provider != "SharePoint" || string.IsNullOrWhiteSpace(value.File.Sha256)
            || value.File.Sha256.Length != 64 || value.File.UploadedAt is null)
            throw new ArgumentException("Los metadatos técnicos del archivo están incompletos.");
        if ((value.CommentId.HasValue ? 1 : 0) + (value.FieldResponseId.HasValue ? 1 : 0) + (value.ManagementFieldResponseId.HasValue ? 1 : 0) > 1)
            throw new ArgumentException("Un adjunto no puede pertenecer simultáneamente a un comentario y a una respuesta de campo.");
        if(value.ManagementFieldResponseId.HasValue&&!value.ManagementId.HasValue)
            throw new ArgumentException("La respuesta de campo de gestión requiere identificar la gestión.");
    }
}

public sealed record PersistSolicitudesAttachment(
    Guid Id,
    Guid RequestId,
    Guid? CommentId,
    Guid? FieldResponseId,
    Guid UploadedByThirdPartyId,
    AttachmentVisibility Visibility,
    StoredFile File,
    Guid? ManagementId = null,
    Guid? ManagementFieldResponseId = null);

public sealed record SolicitudesAttachment(
    Guid Id,
    Guid RequestId,
    Guid? CommentId,
    Guid? FieldResponseId,
    Guid UploadedByThirdPartyId,
    AttachmentVisibility Visibility,
    StoredFile File,
    bool IsActive,
    Guid? ManagementId = null,
    Guid? ManagementFieldResponseId = null);

/// <summary>Persists only file metadata. Binary content never belongs in Dataverse.</summary>
public interface ISolicitudesAttachmentStore
{
    Task<SolicitudesAttachment> CreateAsync(PersistSolicitudesAttachment attachment, CancellationToken cancellationToken);
    Task<SolicitudesAttachment?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<SolicitudesAttachment>> ListByRequestAsync(Guid requestId, CancellationToken cancellationToken);
    Task DeactivateAsync(Guid id, CancellationToken cancellationToken);
}

public sealed record SolicitudesAttachmentPolicy(
    bool RequestExists,
    bool IsRequester,
    bool IsManager,
    bool AttachmentsAllowed,
    int MaximumAttachments,
    int CurrentAttachments,
    long MaximumFileBytes);

public interface ISolicitudesAttachmentPolicyStore
{
    Task<SolicitudesAttachmentPolicy> ReadAsync(Guid requestId, Guid actorThirdPartyId, Guid? managementId, CancellationToken cancellationToken);
}

public enum SolicitudesHistoryMovement { StateChanged = 299540103, CommentAdded = 299540106, FileAdded = 299540107, FileDeactivated = 299540108 }
public enum SolicitudesHistoryOrigin { RequesterPortal = 299540120, SolicitudesAdministration = 299540121, Api = 299540122, System = 299540123 }
public sealed record AppendSolicitudesHistory(Guid RequestId, Guid? ActorThirdPartyId, string OperationId,
    SolicitudesHistoryMovement Movement, SolicitudesHistoryOrigin Origin, bool VisibleToRequester, DateTimeOffset OccurredAt, Guid? PreviousStateId = null, Guid? NewStateId = null);
public interface ISolicitudesHistoryStore
{
    Task AppendAsync(AppendSolicitudesHistory history, CancellationToken cancellationToken);
}
