using Gaia.BuildingBlocks.Files;

namespace Gaia.Modules.Helpdesk;

public sealed record UploadHelpdeskAttachment(
    Guid RequestId,
    Guid? CommentId,
    Guid? FieldResponseId,
    Guid UploadedByThirdPartyId,
    AttachmentVisibility Visibility,
    string OriginalName,
    string ContentType,
    long Length,
    Guid? ManagementId = null,
    Guid? ManagementFieldResponseId = null);

public interface IHelpdeskAttachmentApplication
{
    Task<HelpdeskAttachment> UploadAsync(UploadHelpdeskAttachment request, Stream content, CancellationToken cancellationToken);
    Task<IReadOnlyList<HelpdeskAttachment>> ListAsync(Guid requestId, Guid actorThirdPartyId, CancellationToken cancellationToken);
    Task<FileDownload> DownloadAsync(Guid attachmentId, Guid actorThirdPartyId, CancellationToken cancellationToken);
    Task DeactivateAsync(Guid attachmentId, Guid actorThirdPartyId, CancellationToken cancellationToken);
    Task RollbackUploadAsync(HelpdeskAttachment attachment, Guid actorThirdPartyId, CancellationToken cancellationToken);
    Task<HelpdeskAttachmentReconciliation> ReconcileAsync(Guid requestId, Guid actorThirdPartyId, CancellationToken cancellationToken);
}

public sealed class HelpdeskAttachmentApplication(
    IFileStorage storage,
    IFileStorageMaintenance maintenance,
    IHelpdeskAttachmentStore attachments,
    IHelpdeskAttachmentPolicyStore policies,
    IHelpdeskHistoryStore history,
    TimeProvider timeProvider) : IHelpdeskAttachmentApplication
{
    public async Task<HelpdeskAttachment> UploadAsync(UploadHelpdeskAttachment request, Stream content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(content);
        if (request.RequestId == Guid.Empty || request.UploadedByThirdPartyId == Guid.Empty)
            throw new ArgumentException("Los identificadores obligatorios del adjunto no son válidos.");
        var policy = await policies.ReadAsync(request.RequestId, request.UploadedByThirdPartyId, cancellationToken);
        ValidatePolicy(request, policy);

        var uploadedAt = timeProvider.GetUtcNow();
        StoredFile? stored = null;
        HelpdeskAttachment? created = null;
        var attachmentId = Guid.Empty;
        try
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                attachmentId = Guid.NewGuid();
                if (content.CanSeek) content.Position = 0;
                try
                {
                    stored = await storage.UploadAsync(new(
                        new(request.RequestId.ToString("D")),
                        request.OriginalName,
                        request.ContentType,
                        request.Length,
                        attachmentId,
                        uploadedAt), content, cancellationToken);
                    break;
                }
                catch (FileStorageException error) when (error.Code == FileStorageError.VersionConflict && attempt < 2)
                {
                    continue;
                }
            }
            if (stored is null) throw new FileStorageException(FileStorageError.VersionConflict);
            created = await attachments.CreateAsync(new(
                attachmentId,
                request.RequestId,
                request.CommentId,
                request.FieldResponseId,
                request.UploadedByThirdPartyId,
                request.Visibility,
                stored,
                request.ManagementId,
                request.ManagementFieldResponseId), cancellationToken);
            var historyEntry = new AppendHelpdeskHistory(created.RequestId, created.UploadedByThirdPartyId, attachmentId.ToString("D"),
                HelpdeskHistoryMovement.FileAdded, policy.IsManager ? HelpdeskHistoryOrigin.HelpdeskAdministration : HelpdeskHistoryOrigin.RequesterPortal,
                created.Visibility == AttachmentVisibility.Requester, uploadedAt);
            for (var attempt = 0; ; attempt++)
            {
                try
                {
                    await history.AppendAsync(historyEntry, cancellationToken);
                    break;
                }
                catch (InvalidOperationException) when (attempt < 2)
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(250 * (attempt + 1)), cancellationToken);
                }
            }
            return created;
        }
        catch when (stored is not null)
        {
            if (created is not null)
            {
                try
                {
                    await attachments.DeactivateAsync(created.Id, CancellationToken.None);
                }
                catch
                {
                    throw new InvalidOperationException("No fue posible compensar el registro del adjunto; se requiere conciliación.");
                }
            }
            try
            {
                await maintenance.DeletePhysicallyAsync(new(stored.Id, stored.ETag,
                    "Cleanup of uncommitted Helpdesk attachment"), CancellationToken.None);
            }
            catch (FileStorageException)
            {
                throw new InvalidOperationException("No fue posible confirmar ni compensar la carga; se requiere conciliación.");
            }
            throw;
        }
    }

    public async Task<IReadOnlyList<HelpdeskAttachment>> ListAsync(Guid requestId, Guid actorThirdPartyId, CancellationToken cancellationToken)
    {
        var policy = await policies.ReadAsync(requestId, actorThirdPartyId, cancellationToken);
        RequireAccess(policy);
        var items = await attachments.ListByRequestAsync(requestId, cancellationToken);
        return policy.IsManager ? items : items.Where(item => item.Visibility == AttachmentVisibility.Requester).ToArray();
    }

    public async Task<FileDownload> DownloadAsync(Guid attachmentId, Guid actorThirdPartyId, CancellationToken cancellationToken)
    {
        var attachment = await attachments.GetAsync(attachmentId, cancellationToken)
            ?? throw new KeyNotFoundException("El adjunto no existe.");
        var policy = await policies.ReadAsync(attachment.RequestId, actorThirdPartyId, cancellationToken);
        RequireAccess(policy);
        if (!policy.IsManager && attachment.Visibility != AttachmentVisibility.Requester)
            throw new UnauthorizedAccessException("No tiene acceso a este adjunto.");
        return await storage.DownloadAsync(attachment.File.Id, cancellationToken);
    }

    public async Task DeactivateAsync(Guid attachmentId, Guid actorThirdPartyId, CancellationToken cancellationToken)
    {
        var attachment = await attachments.GetAsync(attachmentId, cancellationToken)
            ?? throw new KeyNotFoundException("El adjunto no existe.");
        var policy = await policies.ReadAsync(attachment.RequestId, actorThirdPartyId, cancellationToken);
        RequireAccess(policy);
        if (!policy.IsManager && attachment.UploadedByThirdPartyId != actorThirdPartyId)
            throw new UnauthorizedAccessException("Solo puede retirar sus propios adjuntos.");
        await attachments.DeactivateAsync(attachmentId, cancellationToken);
        await history.AppendAsync(new(attachment.RequestId, actorThirdPartyId, attachmentId.ToString("D"),
            HelpdeskHistoryMovement.FileDeactivated, policy.IsManager ? HelpdeskHistoryOrigin.HelpdeskAdministration : HelpdeskHistoryOrigin.RequesterPortal,
            attachment.Visibility == AttachmentVisibility.Requester, timeProvider.GetUtcNow()), cancellationToken);
    }

    public async Task RollbackUploadAsync(HelpdeskAttachment attachment, Guid actorThirdPartyId, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(attachment);
        if (!attachment.IsActive || attachment.UploadedByThirdPartyId != actorThirdPartyId)
            throw new UnauthorizedAccessException("No está autorizado para compensar este adjunto.");
        await attachments.DeactivateAsync(attachment.Id, cancellationToken);
        try
        {
            await maintenance.DeletePhysicallyAsync(new(attachment.File.Id, attachment.File.ETag,
                "Rollback of incomplete Helpdesk observation response"), CancellationToken.None);
        }
        catch (FileStorageException)
        {
            throw new InvalidOperationException("El adjunto quedó inactivo, pero su archivo requiere conciliación.");
        }
    }

    public async Task<HelpdeskAttachmentReconciliation> ReconcileAsync(Guid requestId, Guid actorThirdPartyId, CancellationToken cancellationToken)
    {
        var policy = await policies.ReadAsync(requestId, actorThirdPartyId, cancellationToken);
        RequireAccess(policy);
        if (!policy.IsManager) throw new UnauthorizedAccessException("Solo un gestor puede conciliar adjuntos.");
        var records = await attachments.ListByRequestAsync(requestId, cancellationToken);
        var files = await storage.ListAsync(new(requestId.ToString("D")), cancellationToken);
        var recordIds = records.Select(item => item.File.Id.FileId).ToHashSet(StringComparer.Ordinal);
        var fileIds = files.Select(item => item.Id.FileId).ToHashSet(StringComparer.Ordinal);
        return new(requestId,
            records.Where(item => !fileIds.Contains(item.File.Id.FileId)).Select(item => item.Id).ToArray(),
            files.Where(item => !recordIds.Contains(item.Id.FileId)).Select(item => item.Id).ToArray());
    }

    private static void RequireAccess(HelpdeskAttachmentPolicy policy)
    {
        if (!policy.RequestExists) throw new KeyNotFoundException("La solicitud no existe o está inactiva.");
        if (!policy.IsRequester && !policy.IsManager) throw new UnauthorizedAccessException("No tiene acceso a esta solicitud.");
    }

    private static void ValidatePolicy(UploadHelpdeskAttachment request, HelpdeskAttachmentPolicy policy)
    {
        if (!policy.RequestExists) throw new KeyNotFoundException("La solicitud no existe o está inactiva.");
        if (!policy.IsRequester && !policy.IsManager) throw new UnauthorizedAccessException("No tiene acceso a esta solicitud.");
        if (!policy.AttachmentsAllowed) throw new InvalidOperationException("El servicio no permite adjuntos.");
        if (policy.MaximumAttachments <= 0 || policy.CurrentAttachments >= policy.MaximumAttachments)
            throw new InvalidOperationException("La solicitud alcanzó el máximo de adjuntos permitido.");
        if (request.Length <= 0 || request.Length > policy.MaximumFileBytes)
            throw new ArgumentException("El archivo supera el tamaño permitido por el servicio.");
        if (request.Visibility == AttachmentVisibility.Internal && !policy.IsManager)
            throw new UnauthorizedAccessException("Solo un gestor puede cargar adjuntos internos.");
    }
}

public sealed record HelpdeskAttachmentReconciliation(Guid RequestId, IReadOnlyList<Guid> MissingFiles,
    IReadOnlyList<ExternalFileId> OrphanFiles);
