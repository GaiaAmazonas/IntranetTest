using System.Security.Cryptography;
using Gaia.BuildingBlocks.Files;

namespace Gaia.Api.Infrastructure.Files;

internal sealed class FileStorageMigration(IFileStorage storage, IFileStorageMaintenance maintenance,
    TimeProvider timeProvider) : IFileStorageMigration
{
    public async Task<FileMigrationReceipt> CopyAndVerifyAsync(FileMigration migration, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(migration);
        if (string.IsNullOrWhiteSpace(migration.ExpectedSourceETag))
            throw new FileStorageException(FileStorageError.VersionConflict);

        var source = await storage.GetMetadataAsync(migration.Source, cancellationToken);
        if (!string.Equals(source.ETag, migration.ExpectedSourceETag, StringComparison.Ordinal))
            throw new FileStorageException(FileStorageError.VersionConflict);

        StoredFile? destination = null;
        try
        {
            await using var download = await storage.DownloadAsync(migration.Source, cancellationToken);
            await using var staged = await FileTransferStreams.PrepareAsync(download.Content, source.Length, cancellationToken);
            var sourceHash = await SHA256.HashDataAsync(staged, cancellationToken);
            staged.Position = 0;
            destination = await storage.UploadAsync(new(migration.DestinationScope, migration.OriginalName,
                source.ContentType, source.Length, migration.DestinationFileId, timeProvider.GetUtcNow()), staged, cancellationToken);
            if (destination.Id.RepositoryId == source.Id.RepositoryId
                && destination.Id.ContainerId == source.Id.ContainerId)
                throw new FileStorageException(FileStorageError.FileUnauthorized);

            await using var verification = await storage.DownloadAsync(destination.Id, cancellationToken);
            var destinationHash = await SHA256.HashDataAsync(verification.Content, cancellationToken);
            if (verification.Metadata.Length != source.Length || !sourceHash.AsSpan().SequenceEqual(destinationHash))
                throw new FileStorageException(FileStorageError.VersionConflict);
            return new(source.Id, source.ETag, destination, timeProvider.GetUtcNow());
        }
        catch
        {
            if (destination is not null)
            {
                try
                {
                    await maintenance.DeletePhysicallyAsync(new(destination.Id, destination.ETag,
                        "Cleanup of uncommitted repository migration"), CancellationToken.None);
                }
                catch (FileStorageException)
                {
                    throw new FileStorageException(FileStorageError.TransientFailure);
                }
            }
            throw;
        }
    }
}
