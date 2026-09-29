using Gaia.BuildingBlocks.Files;
using Gaia.Modules.Solicitudes;

namespace Gaia.ArchitectureTests;

public sealed class SolicitudesObservationApplicationTests
{
    [Fact]
    public async Task ResponseWithoutDocumentChangesStateWithoutUploading()
    {
        var attachments = new Attachments();
        var conversation = new Conversation();
        var application = new SolicitudesObservationApplication(attachments, conversation);

        var result = await application.AttendAsync(
            Command() with { OriginalName = null, ContentType = null, Length = 0 }, Stream.Null, default);

        Assert.False(attachments.UploadCalled);
        Assert.True(conversation.TransitionCalled);
        Assert.Null(result.Attachment);
    }

    [Fact]
    public async Task StorageFailureDoesNotSaveResponseOrChangeState()
    {
        var attachments = new Attachments { FailUpload = true };
        var conversation = new Conversation();
        var application = new SolicitudesObservationApplication(attachments, conversation);

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
        var application = new SolicitudesObservationApplication(attachments, conversation);

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
        var application = new SolicitudesObservationApplication(attachments, conversation);

        var result = await application.AttendAsync(Command(), new MemoryStream(new byte[10]), default);

        Assert.Equal("En gestión", result.Request.Status);
        Assert.Equal(attachments.Uploaded.Id, result.Attachment!.Id);
        Assert.False(attachments.RollbackCalled);
    }

    [Theory]
    [InlineData("Radicada")]
    [InlineData("Asignada")]
    [InlineData("En gestión")]
    public async Task ResponsePreservesTheServerSelectedReturnState(string state)
    {
        var attachments = new Attachments();
        var application = new SolicitudesObservationApplication(attachments, new Conversation { ReturnState = state });
        var result = await application.AttendAsync(Command(), new MemoryStream(new byte[10]), default);
        Assert.Equal(state, result.Request.Status);
        Assert.True(attachments.UploadCalled);
        Assert.False(attachments.RollbackCalled);
    }

    private static AttendSolicitudesObservation Command() => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        "Respuesta con soporte documental", "evidencia-observacion.pdf", "application/pdf", 10);

    private sealed class Attachments : ISolicitudesAttachmentApplication
    {
        public bool FailUpload { get; init; }
        public bool UploadCalled { get; private set; }
        public bool RollbackCalled { get; private set; }
        public SolicitudesAttachment Uploaded { get; } = new(Guid.NewGuid(), Guid.NewGuid(), null, null, Guid.NewGuid(),
            AttachmentVisibility.Requester, new(new("SharePoint", "development-local", "solicitudes", Guid.NewGuid().ToString("D")),
                "etag", "evidencia-observacion.pdf", "technical.pdf", "application/pdf", 10,
                Sha256: new string('a', 64), UploadedAt: DateTimeOffset.UtcNow), true);

        public Task<SolicitudesAttachment> UploadAsync(UploadSolicitudesAttachment request, Stream content, CancellationToken cancellationToken)
        {
            UploadCalled = true;
            if (FailUpload) throw new FileStorageException(FileStorageError.MissingConfiguration);
            return Task.FromResult(Uploaded with { RequestId = request.RequestId, UploadedByThirdPartyId = request.UploadedByThirdPartyId });
        }
        public Task RollbackUploadAsync(SolicitudesAttachment attachment, Guid actorThirdPartyId, CancellationToken cancellationToken)
        { RollbackCalled = true; return Task.CompletedTask; }
        public Task<IReadOnlyList<SolicitudesAttachment>> ListAsync(Guid requestId, Guid actorThirdPartyId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<FileDownload> DownloadAsync(Guid attachmentId, Guid actorThirdPartyId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task DeactivateAsync(Guid attachmentId, Guid actorThirdPartyId, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<SolicitudesAttachmentReconciliation> ReconcileAsync(Guid requestId, Guid actorThirdPartyId, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

    private sealed class Conversation : ISolicitudesConversationApplication
    {
        public string ReturnState { get; init; } = "En gestión";
        public bool FailTransition { get; init; }
        public bool TransitionCalled { get; private set; }
        public Task<SolicitudesRequestDetail> TransitionAsync(Guid requestId, Guid actorThirdPartyId,
            ApplySolicitudesTransition request, bool managementAccess, CancellationToken cancellationToken)
        {
            TransitionCalled = true;
            if (FailTransition) throw new InvalidOperationException("Transition failed");
            return Task.FromResult(new SolicitudesRequestDetail(requestId, "HD-TEST", "Asunto", "Descripción", "Servicio",
                ReturnState, null, DateTimeOffset.UtcNow, null, false, false, [], []));
        }
        public Task<SolicitudesRequestDetail> ReadAsync(Guid requestId, Guid actorThirdPartyId, bool managementAccess, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<SolicitudesComment> AddAsync(Guid requestId, Guid actorThirdPartyId, AddSolicitudesComment request, bool managementAccess, CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
