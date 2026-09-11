using Gaia.BuildingBlocks.Files;

namespace Gaia.Modules.Helpdesk;

public enum AttachmentVisibility { Requester = 299540080, Internal = 299540081 }

public static class HelpdeskAttachmentRules
{
    public const int SharePointProvider = 299540090;

    public static void Validate(PersistHelpdeskAttachment value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (value.Id == Guid.Empty || value.RequestId == Guid.Empty || value.UploadedByThirdPartyId == Guid.Empty)
            throw new ArgumentException("Los identificadores obligatorios del adjunto no son válidos.");
        if (value.Visibility is not (AttachmentVisibility.Requester or AttachmentVisibility.Internal))
            throw new ArgumentException("La visibilidad del adjunto no es válida.");
        if (value.File.Id.Provider != "SharePoint" || string.IsNullOrWhiteSpace(value.File.Sha256)
            || value.File.Sha256.Length != 64 || value.File.UploadedAt is null)
            throw new ArgumentException("Los metadatos técnicos del archivo están incompletos.");
        if ((value.CommentId.HasValue ? 1 : 0) + (value.FieldResponseId.HasValue ? 1 : 0) > 1)
            throw new ArgumentException("Un adjunto no puede pertenecer simultáneamente a un comentario y a una respuesta de campo.");
    }
}

public sealed record PersistHelpdeskAttachment(
    Guid Id,
    Guid RequestId,
    Guid? CommentId,
    Guid? FieldResponseId,
    Guid UploadedByThirdPartyId,
    AttachmentVisibility Visibility,
    StoredFile File);

public sealed record HelpdeskAttachment(
    Guid Id,
    Guid RequestId,
    Guid? CommentId,
    Guid? FieldResponseId,
    Guid UploadedByThirdPartyId,
    AttachmentVisibility Visibility,
    StoredFile File,
    bool IsActive);

/// <summary>Persists only file metadata. Binary content never belongs in Dataverse.</summary>
public interface IHelpdeskAttachmentStore
{
    Task<HelpdeskAttachment> CreateAsync(PersistHelpdeskAttachment attachment, CancellationToken cancellationToken);
    Task<HelpdeskAttachment?> GetAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<HelpdeskAttachment>> ListByRequestAsync(Guid requestId, CancellationToken cancellationToken);
    Task DeactivateAsync(Guid id, CancellationToken cancellationToken);
}

public sealed record HelpdeskAttachmentPolicy(
    bool RequestExists,
    bool IsRequester,
    bool IsManager,
    bool AttachmentsAllowed,
    int MaximumAttachments,
    int CurrentAttachments,
    long MaximumFileBytes);

public interface IHelpdeskAttachmentPolicyStore
{
    Task<HelpdeskAttachmentPolicy> ReadAsync(Guid requestId, Guid actorThirdPartyId, CancellationToken cancellationToken);
}

public enum HelpdeskHistoryMovement { StateChanged = 299540103, CommentAdded = 299540106, FileAdded = 299540107, FileDeactivated = 299540108 }
public enum HelpdeskHistoryOrigin { RequesterPortal = 299540120, HelpdeskAdministration = 299540121, Api = 299540122, System = 299540123 }
public sealed record AppendHelpdeskHistory(Guid RequestId, Guid? ActorThirdPartyId, string OperationId,
    HelpdeskHistoryMovement Movement, HelpdeskHistoryOrigin Origin, bool VisibleToRequester, DateTimeOffset OccurredAt);
public interface IHelpdeskHistoryStore
{
    Task AppendAsync(AppendHelpdeskHistory history, CancellationToken cancellationToken);
}
