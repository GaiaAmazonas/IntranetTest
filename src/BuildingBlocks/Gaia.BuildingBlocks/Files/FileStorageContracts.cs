namespace Gaia.BuildingBlocks.Files;

/// <summary>Infrastructure port. Callers must authorize access before invoking it.</summary>
public interface IFileStorage
{
    Task<StoredFile> UploadAsync(FileUpload upload, Stream content, CancellationToken cancellationToken);
    Task<FileDownload> DownloadAsync(ExternalFileId file, CancellationToken cancellationToken);
    Task<StoredFile> GetMetadataAsync(ExternalFileId file, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(ExternalFileId file, CancellationToken cancellationToken);
    Task<IReadOnlyList<StoredFile>> ListAsync(LogicalFileScope scope, CancellationToken cancellationToken);
    Task<ExternalFolder> EnsureFolderAsync(LogicalFileScope scope, CancellationToken cancellationToken);
    Task<FileStorageAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken);
}

/// <summary>Separate port for authorized cleanup, never for ordinary business soft deletion.</summary>
public interface IFileStorageMaintenance
{
    Task DeletePhysicallyAsync(PhysicalFileDeletion deletion, CancellationToken cancellationToken);
}

/// <summary>Copies and verifies a file into the current default repository without deleting the source.</summary>
public interface IFileStorageMigration
{
    Task<FileMigrationReceipt> CopyAndVerifyAsync(FileMigration migration, CancellationToken cancellationToken);
}

public sealed record LogicalFileScope(string Scope, string? CorrelationId = null);
public sealed record ExternalFileId(string Provider, string RepositoryId, string ContainerId, string FileId);
public sealed record ExternalFolder(string Provider, string RepositoryId, string ContainerId, string FolderId, string LogicalPath);
public sealed record FileUpload(LogicalFileScope Scope, string OriginalName, string ContentType, long Length,
    Guid FileId, DateTimeOffset UploadedAt);
public sealed record StoredFile(
    ExternalFileId Id,
    string ETag,
    string? OriginalName,
    string StoredName,
    string ContentType,
    long Length,
    Uri? WebUrl = null,
    string? LogicalPath = null,
    string? Sha256 = null,
    DateTimeOffset? UploadedAt = null);
public sealed record PhysicalFileDeletion(ExternalFileId Id, string ExpectedETag, string Reason);
public sealed record FileMigration(ExternalFileId Source, string ExpectedSourceETag,
    LogicalFileScope DestinationScope, string OriginalName, Guid DestinationFileId);
public sealed record FileMigrationReceipt(ExternalFileId Source, string SourceETag,
    StoredFile Destination, DateTimeOffset CopiedAt);

/// <summary>Content must own the HTTP response resources. The caller disposes the download.</summary>
public sealed class FileDownload(StoredFile metadata, Stream content) : IAsyncDisposable
{
    public StoredFile Metadata { get; } = metadata;
    public Stream Content { get; } = content;
    public ValueTask DisposeAsync() => Content.DisposeAsync();
}

public enum StorageAccessStatus { Unknown, Available, Unavailable }
public sealed record FileStorageAvailability(
    StorageAccessStatus Authentication,
    StorageAccessStatus Repository,
    StorageAccessStatus Container,
    StorageAccessStatus RootFolder,
    StorageAccessStatus Read,
    StorageAccessStatus Write);

/// <summary>Streaming scan hook. There is intentionally no default implementation declaring files clean.</summary>
public interface IFileContentScanner
{
    Task<FileScanResult> ScanAsync(FileUpload upload, Stream content, CancellationToken cancellationToken);
}
public enum FileScanStatus { Clean, Rejected, Unavailable }
public sealed record FileScanResult(FileScanStatus Status);

public enum FileStorageError
{
    MissingConfiguration, InvalidCredentials, RepositoryInaccessible, ContainerInaccessible,
    FileNotFound, FileUnauthorized, FileTooLarge, TypeNotAllowed, InvalidFileName,
    InvalidLogicalPath, InvalidLength, VersionConflict, TransientFailure, ScanRejected, ScanUnavailable
}

/// <summary>Only controlled messages: never provider bodies, tokens or signed URLs.</summary>
public sealed class FileStorageException(FileStorageError code) : Exception(MessageFor(code))
{
    public FileStorageError Code { get; } = code;

    private static string MessageFor(FileStorageError code) => code switch
    {
        FileStorageError.MissingConfiguration => "El almacenamiento de archivos no está configurado.",
        FileStorageError.InvalidCredentials => "No fue posible autenticar el servicio de archivos.",
        FileStorageError.RepositoryInaccessible => "El repositorio de archivos no está disponible para la aplicación.",
        FileStorageError.ContainerInaccessible => "La biblioteca de archivos no está disponible para la aplicación.",
        FileStorageError.FileNotFound => "No se encontró el archivo.",
        FileStorageError.FileUnauthorized => "No está autorizado el acceso al archivo.",
        FileStorageError.FileTooLarge => "El archivo supera el tamaño permitido.",
        FileStorageError.TypeNotAllowed => "El tipo de archivo no está permitido.",
        FileStorageError.InvalidFileName => "El nombre de archivo no es válido.",
        FileStorageError.InvalidLogicalPath => "La ubicación lógica del archivo no es válida.",
        FileStorageError.InvalidLength => "El tamaño del archivo no es válido.",
        FileStorageError.VersionConflict => "El archivo cambió o existe un conflicto de versión.",
        FileStorageError.ScanRejected => "El archivo fue rechazado por la revisión de seguridad.",
        FileStorageError.ScanUnavailable => "No fue posible completar la revisión de seguridad del archivo.",
        _ => "El servicio de archivos no está disponible temporalmente."
    };
}
