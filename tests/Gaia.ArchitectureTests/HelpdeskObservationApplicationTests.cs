using Gaia.BuildingBlocks.Files;
using Gaia.Modules.Helpdesk;

namespace Gaia.ArchitectureTests;

public sealed class HelpdeskObservationApplicationTests
{
    [Fact]
    public async Task DocumentIsMandatoryBeforeAnyMutation()
    {
        var attachments = new Attachments();
        var conversation = new Conversation();
        var application = new HelpdeskObservationApplication(attachments, conversation);

        await Assert.ThrowsAsync<ArgumentException>(() => application.AttendAsync(
            Command() with { Length = 0 }, new MemoryStream(), default));

        Assert.False(attachments.UploadCalled);
        Assert.False(conversation.TransitionCalled);
    }

    [Fact]
    public async Task StorageFailureDoesNotSaveResponseOrChangeState()
    {
        var attachments = new Attachments { FailUpload = true };
        var conversation = new Conversation();
        var application = new HelpdeskObservationApplication(attachments, conversation);

        await Assert.ThrowsAsync<FileStorageException>(() => application.AttendAsync(
            Command(), new MemoryStream(new byte[10]), default));

        Assert.False(conversation.TransitionCalled);
        Assert.False(attachments.RollbackCalled);
    }

    [Fact]
    public async Task TransitionFailureCompensatesUploadedDocument()
    {
        var attachments = new Attachments();
        var conversation = new Conversation { FailTransition = true };
        var application = new HelpdeskObservationApplication(attachments, conversation);

        await Assert.ThrowsAsync<InvalidOperationException>(() => application.AttendAsync(
            Command(), new MemoryStream(new byte[10]), default));

        Assert.True(attachments.UploadCalled);
        Assert.True(conversation.TransitionCalled);
        Assert.True(attachments.RollbackCalled);
    }

    [Fact]
    public async Task SuccessfulOperationReturnsAttachmentAndManagementState()
    {
        var attachments = new Attachments();
        var conversation = new Conversation();
        var application = new HelpdeskObservationApplication(attachments, conversation);

        var result = await application.AttendAsync(Command(), new MemoryStream(new byte[10]), default);

        Assert.Equal("En gestión", result.Request.Status);
        Assert.Equal(attachments.Uploaded.Id, result.Attachment.Id);
        Assert.False(attachments.RollbackCalled);
    }

    private static AttendHelpdeskObservation Command() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        "Respuesta con soporte documental", "evidencia-observacion.pdf", "application/pdf", 10);

    private sealed class Attachments : IHelpdeskAttachmentApplication
    {
        public bool FailUpload { get; init; }
        public bool UploadCalled { get; private set; }
        public bool RollbackCalled { get; private set; }
        public HelpdeskAttachment Uploaded { get; } = new(Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid(),
            AttachmentVisibility.Requester, new(new("SharePoint", "development-local", "helpdesk", Guid.NewGuid().ToString("D")),
                "etag", "evidencia-observacion.pdf", "technical.pdf", "application/pdf", 10,
                Sha256: new string('a', 64), UploadedAt: DateTimeOffset.UtcNow), true);

        public Task<HelpdeskAttachment> UploadAsync(UploadHelpdeskAttachment request, Stream content, CancellationToken cancellationToken)
        {
            UploadCalled = true;
            if (FailUpload) throw new FileStorageException(FileStorageError.MissingConfiguration);
            return Task.FromResult(Uploaded with { RequestId = request.RequestId, UploadedByThirdPartyId = request.UploadedByThirdPartyId });
        }
        public Task RollbackUploadAsync(HelpdeskAttachment attachment, Guid actorThirdPartyId, CancellationToken cancellationToken)
        { RollbackCalled = true; return Task.CompletedTask; }
        public Task<IReadOnlyList<HelpdeskAttachment>> ListAsync(Guid requestId, Guid actorThirdPartyId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<FileDownload> DownloadAsync(Guid attachmentId, Guid actorThirdPartyId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeactivateAsync(Guid attachmentId, Guid actorThirdPartyId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<HelpdeskAttachmentReconciliation> ReconcileAsync(Guid requestId, Guid actorThirdPartyId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class Conversation : IHelpdeskConversationApplication
    {
        public bool FailTransition { get; init; }
        public bool TransitionCalled { get; private set; }
        public Task<HelpdeskRequestDetail> TransitionAsync(Guid requestId, Guid actorThirdPartyId,
            ApplyHelpdeskTransition request, bool managementAccess, CancellationToken cancellationToken)
        {
            TransitionCalled = true;
            if (FailTransition) throw new InvalidOperationException("Transition failed");
            return Task.FromResult(new HelpdeskRequestDetail(requestId, "HD-TEST", "Asunto", "Descripción", "Servicio",
                "En gestión", null, DateTimeOffset.UtcNow, null, false, false, [], []));
        }
        public Task<HelpdeskRequestDetail> ReadAsync(Guid requestId, Guid actorThirdPartyId, bool managementAccess, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<HelpdeskComment> AddAsync(Guid requestId, Guid actorThirdPartyId, AddHelpdeskComment request, bool managementAccess, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
