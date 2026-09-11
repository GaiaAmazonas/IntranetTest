using Gaia.BuildingBlocks.Files;
using Gaia.Modules.Helpdesk;

namespace Gaia.ArchitectureTests;

public sealed class HelpdeskAttachmentApplicationTests
{
    [Fact]
    public async Task UploadUsesStableIdAndPersistsReturnedStorageMetadata()
    {
        var fake = new Dependencies();
        var service = new HelpdeskAttachmentApplication(fake, fake, fake, fake, fake, TimeProvider.System);
        await using var content = new MemoryStream(new byte[10]);
        var result = await service.UploadAsync(Request(), content, default);

        Assert.Equal(fake.Upload!.FileId, fake.Persisted!.Id);
        Assert.Equal(fake.Stored with { UploadedAt = fake.Upload.UploadedAt }, fake.Persisted.File);
        Assert.Equal(result.Id, fake.Persisted.Id);
        Assert.Equal(result.RequestId.ToString("D"), fake.Upload.Scope.Scope);
        Assert.Null(fake.Upload.Scope.CorrelationId);
        Assert.Empty(fake.Deleted);
        Assert.Equal(HelpdeskHistoryMovement.FileAdded, Assert.Single(fake.History).Movement);
    }

    [Fact]
    public async Task DataverseFailureCompensatesExactUploadedVersion()
    {
        var fake = new Dependencies { FailPersistence = true };
        var service = new HelpdeskAttachmentApplication(fake, fake, fake, fake, fake, TimeProvider.System);
        await using var content = new MemoryStream(new byte[10]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadAsync(Request(), content, default));
        var deletion = Assert.Single(fake.Deleted);
        Assert.Equal(fake.Stored.Id, deletion.Id);
        Assert.Equal(fake.Stored.ETag, deletion.ExpectedETag);
    }

    [Fact]
    public async Task HistoryFailureDeactivatesRecordBeforeDeletingPhysicalFile()
    {
        var fake = new Dependencies { FailHistory = true };
        var service = new HelpdeskAttachmentApplication(fake, fake, fake, fake, fake, TimeProvider.System);
        await using var content = new MemoryStream(new byte[10]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadAsync(Request(), content, default));

        Assert.Equal(fake.Persisted!.Id, Assert.Single(fake.Deactivated));
        Assert.Single(fake.Deleted);
    }

    [Fact]
    public async Task RequesterCannotUploadInternalAttachment()
    {
        var fake = new Dependencies { Policy = Allowed() with { IsManager = false, IsRequester = true } };
        var service = new HelpdeskAttachmentApplication(fake, fake, fake, fake, fake, TimeProvider.System);
        await using var content = new MemoryStream(new byte[10]);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.UploadAsync(
            Request() with { Visibility = AttachmentVisibility.Internal }, content, default));
        Assert.Null(fake.Upload);
    }

    [Fact]
    public async Task LimitIsCheckedBeforeWritingToSharePoint()
    {
        var fake = new Dependencies { Policy = Allowed() with { CurrentAttachments = 3, MaximumAttachments = 3 } };
        var service = new HelpdeskAttachmentApplication(fake, fake, fake, fake, fake, TimeProvider.System);
        await using var content = new MemoryStream(new byte[10]);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.UploadAsync(Request(), content, default));
        Assert.Null(fake.Upload);
    }

    [Fact]
    public async Task RequesterListDoesNotExposeInternalAttachments()
    {
        var requester = Guid.NewGuid();
        var fake = new Dependencies
        {
            Listed = [Attachment(AttachmentVisibility.Requester, requester), Attachment(AttachmentVisibility.Internal, Guid.NewGuid())]
        };
        var service = new HelpdeskAttachmentApplication(fake, fake, fake, fake, fake, TimeProvider.System);
        var result = await service.ListAsync(Guid.NewGuid(), requester, default);
        Assert.Equal(AttachmentVisibility.Requester, Assert.Single(result).Visibility);
    }

    [Fact]
    public async Task RequesterCannotDeactivateAnotherUsersAttachment()
    {
        var fake = new Dependencies { Current = Attachment(AttachmentVisibility.Requester, Guid.NewGuid()) };
        var service = new HelpdeskAttachmentApplication(fake, fake, fake, fake, fake, TimeProvider.System);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.DeactivateAsync(fake.Current.Id, Guid.NewGuid(), default));
        Assert.Empty(fake.Deactivated);
    }

    [Fact]
    public async Task ManagerReconciliationReportsMissingAndOrphanReferencesWithoutDeleting()
    {
        var referenced = Attachment(AttachmentVisibility.Internal, Guid.NewGuid());
        var orphan = referenced.File with { Id = referenced.File.Id with { FileId = "orphan" } };
        var fake = new Dependencies
        {
            Policy = Allowed() with { IsRequester = false, IsManager = true },
            Listed = [referenced], PhysicalFiles = [orphan]
        };
        var service = new HelpdeskAttachmentApplication(fake, fake, fake, fake, fake, TimeProvider.System);
        var report = await service.ReconcileAsync(referenced.RequestId, Guid.NewGuid(), default);
        Assert.Equal(referenced.Id, Assert.Single(report.MissingFiles));
        Assert.Equal("orphan", Assert.Single(report.OrphanFiles).FileId);
        Assert.Empty(fake.Deleted);
    }

    private static UploadHelpdeskAttachment Request() => new(Guid.NewGuid(), null, null, Guid.NewGuid(),
        AttachmentVisibility.Requester, "evidence.pdf", "application/pdf", 10);
    private static HelpdeskAttachmentPolicy Allowed() => new(true, true, false, true, 5, 0, 10 * 1024 * 1024);
    private static HelpdeskAttachment Attachment(AttachmentVisibility visibility, Guid uploadedBy) =>
        new(Guid.NewGuid(), Guid.NewGuid(), null, null, uploadedBy, visibility,
            new(new("SharePoint", "site", "drive", "item"), "\"etag\"", "evidence.pdf", "technical.pdf",
                "application/pdf", 10, Sha256: new string('a', 64), UploadedAt: DateTimeOffset.UtcNow), true);

    private sealed class Dependencies : IFileStorage, IFileStorageMaintenance, IHelpdeskAttachmentStore,
        IHelpdeskAttachmentPolicyStore, IHelpdeskHistoryStore
    {
        public FileUpload? Upload { get; private set; }
        public PersistHelpdeskAttachment? Persisted { get; private set; }
        public bool FailPersistence { get; init; }
        public bool FailHistory { get; init; }
        public HelpdeskAttachmentPolicy Policy { get; init; } = Allowed();
        public List<PhysicalFileDeletion> Deleted { get; } = [];
        public List<Guid> Deactivated { get; } = [];
        public List<AppendHelpdeskHistory> History { get; } = [];
        public IReadOnlyList<HelpdeskAttachment> Listed { get; init; } = [];
        public HelpdeskAttachment? Current { get; init; }
        public IReadOnlyList<StoredFile> PhysicalFiles { get; init; } = [];
        public StoredFile Stored { get; } = new(new("SharePoint", "site", "drive", "item"), "\"etag\"",
            "evidence.pdf", "technical.pdf", "application/pdf", 10, Sha256: new string('a', 64), UploadedAt: DateTimeOffset.UtcNow);

        public Task<StoredFile> UploadAsync(FileUpload upload, Stream content, CancellationToken cancellationToken)
        { Upload = upload; return Task.FromResult(Stored with { UploadedAt = upload.UploadedAt }); }
        public Task<HelpdeskAttachment> CreateAsync(PersistHelpdeskAttachment attachment, CancellationToken cancellationToken)
        {
            Persisted = attachment;
            if (FailPersistence) throw new InvalidOperationException("Dataverse failure");
            return Task.FromResult(new HelpdeskAttachment(attachment.Id, attachment.RequestId, attachment.CommentId,
                attachment.FieldResponseId, attachment.UploadedByThirdPartyId, attachment.Visibility, attachment.File, true));
        }
        public Task DeletePhysicallyAsync(PhysicalFileDeletion deletion, CancellationToken cancellationToken)
        { Deleted.Add(deletion); return Task.CompletedTask; }
        public Task<bool> ExistsAsync(ExternalFileId file, CancellationToken cancellationToken) => Task.FromResult(true);
        public Task<FileDownload> DownloadAsync(ExternalFileId file, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<StoredFile> GetMetadataAsync(ExternalFileId file, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<ExternalFolder> EnsureFolderAsync(LogicalFileScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<FileStorageAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<IReadOnlyList<StoredFile>> ListAsync(LogicalFileScope scope, CancellationToken cancellationToken) =>
            Task.FromResult(PhysicalFiles);
        public Task<HelpdeskAttachment?> GetAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Current);
        public Task<IReadOnlyList<HelpdeskAttachment>> ListByRequestAsync(Guid requestId, CancellationToken cancellationToken) => Task.FromResult(Listed);
        public Task DeactivateAsync(Guid id, CancellationToken cancellationToken) { Deactivated.Add(id); return Task.CompletedTask; }
        public Task<HelpdeskAttachmentPolicy> ReadAsync(Guid requestId, Guid actorThirdPartyId, CancellationToken cancellationToken) =>
            Task.FromResult(Policy);
        public Task AppendAsync(AppendHelpdeskHistory history, CancellationToken cancellationToken)
        {
            History.Add(history);
            return FailHistory
                ? Task.FromException(new InvalidOperationException("History failure"))
                : Task.CompletedTask;
        }
    }
}
