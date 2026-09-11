using Gaia.Api.Infrastructure.Files;
using Gaia.BuildingBlocks.Files;

namespace Gaia.ArchitectureTests;

public sealed class FileStorageMigrationTests
{
    private static readonly ExternalFileId SourceId = new("SharePoint", "old-site", "old-drive", "old-file");
    private static readonly ExternalFileId DestinationId = new("SharePoint", "new-site", "new-drive", "new-file");

    [Fact]
    public async Task CopyVerifiesContentAndLeavesSourceForTransactionalCutover()
    {
        var storage = new Storage(corruptDestination: false);
        var receipt = await new FileStorageMigration(storage, storage, TimeProvider.System).CopyAndVerifyAsync(
            new(SourceId, "\"source-etag\"", new("Helpdesk", "request-id"), "evidence.pdf", Guid.NewGuid()), default);
        Assert.Equal(SourceId, receipt.Source);
        Assert.Equal(DestinationId, receipt.Destination.Id);
        Assert.Empty(storage.Deleted);
    }

    [Fact]
    public async Task FailedVerificationDeletesOnlyUncommittedDestination()
    {
        var storage = new Storage(corruptDestination: true);
        var error = await Assert.ThrowsAsync<FileStorageException>(() =>
            new FileStorageMigration(storage, storage, TimeProvider.System).CopyAndVerifyAsync(
                new(SourceId, "\"source-etag\"", new("Helpdesk", "request-id"), "evidence.pdf", Guid.NewGuid()), default));
        Assert.Equal(FileStorageError.VersionConflict, error.Code);
        Assert.Equal(DestinationId, Assert.Single(storage.Deleted));
    }

    [Fact]
    public async Task SourceVersionConflictStopsBeforeCopy()
    {
        var storage = new Storage(corruptDestination: false);
        var error = await Assert.ThrowsAsync<FileStorageException>(() =>
            new FileStorageMigration(storage, storage, TimeProvider.System).CopyAndVerifyAsync(
                new(SourceId, "\"stale\"", new("Helpdesk", "request-id"), "evidence.pdf", Guid.NewGuid()), default));
        Assert.Equal(FileStorageError.VersionConflict, error.Code);
        Assert.Equal(0, storage.Uploads);
    }

    private sealed class Storage(bool corruptDestination) : IFileStorage, IFileStorageMaintenance
    {
        private static readonly byte[] Source = [1, 2, 3, 4, 5];
        private static StoredFile SourceMetadata => new(SourceId, "\"source-etag\"", "evidence.pdf", "old.pdf", "application/pdf", Source.Length);
        private static StoredFile DestinationMetadata => new(DestinationId, "\"destination-etag\"", "evidence.pdf", "new.pdf", "application/pdf", Source.Length);
        public List<ExternalFileId> Deleted { get; } = [];
        public int Uploads { get; private set; }

        public Task<StoredFile> GetMetadataAsync(ExternalFileId file, CancellationToken cancellationToken) =>
            Task.FromResult(file == SourceId ? SourceMetadata : DestinationMetadata);
        public Task<bool> ExistsAsync(ExternalFileId file, CancellationToken cancellationToken) =>
            Task.FromResult(file == SourceId || file == DestinationId);
        public Task<IReadOnlyList<StoredFile>> ListAsync(LogicalFileScope scope, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<StoredFile>>([]);
        public Task<FileDownload> DownloadAsync(ExternalFileId file, CancellationToken cancellationToken)
        {
            var bytes = file == SourceId ? Source : corruptDestination ? new byte[] { 1, 2, 3, 4, 6 } : Source;
            return Task.FromResult(new FileDownload(file == SourceId ? SourceMetadata : DestinationMetadata,
                new MemoryStream(bytes, writable: false)));
        }
        public async Task<StoredFile> UploadAsync(FileUpload upload, Stream content, CancellationToken cancellationToken)
        {
            Uploads++;
            using var copy = new MemoryStream();
            await content.CopyToAsync(copy, cancellationToken);
            Assert.Equal(Source, copy.ToArray());
            return DestinationMetadata;
        }
        public Task DeletePhysicallyAsync(PhysicalFileDeletion deletion, CancellationToken cancellationToken)
        { Deleted.Add(deletion.Id); return Task.CompletedTask; }
        public Task<ExternalFolder> EnsureFolderAsync(LogicalFileScope scope, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task<FileStorageAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}
