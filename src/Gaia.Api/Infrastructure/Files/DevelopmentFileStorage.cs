using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Gaia.BuildingBlocks.Files;

namespace Gaia.Api.Infrastructure.Files;

// Development-only durable provider. Production remains bound to the configured SharePoint repository.
internal sealed class DevelopmentFileStorage(IHostEnvironment environment)
    : IFileStorage, IFileStorageMaintenance, IFileStorageDiagnostics
{
    private const long MaximumBytes = 2L * 1024 * 1024 * 1024;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".pdf", ".png", ".jpg", ".jpeg", ".docx", ".xlsx" };
    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf", "image/png", "image/jpeg",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    };
    private readonly string root = Path.Combine(environment.ContentRootPath, "App_Data", "helpdesk-files");
    private string MetadataRoot => Path.Combine(root, ".metadata");

    public async Task<StoredFile> UploadAsync(FileUpload upload, Stream content, CancellationToken token)
    {
        var storedName = FileUploadRules.ValidateAndCreateStoredName(
            upload, MaximumBytes, AllowedExtensions, AllowedContentTypes);
        Directory.CreateDirectory(root);
        Directory.CreateDirectory(MetadataRoot);
        var path = Path.Combine(root, storedName);
        var metadataPath = MetadataPath(upload.FileId);
        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long length = 0;
            await using (var target = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
            {
                var buffer = new byte[81920];
                int read;
                while ((read = await content.ReadAsync(buffer, token)) > 0)
                {
                    await target.WriteAsync(buffer.AsMemory(0, read), token);
                    hash.AppendData(buffer, 0, read);
                    length += read;
                }
            }
            if (length != upload.Length) throw new FileStorageException(FileStorageError.InvalidLength);
            var info = new FileInfo(path);
            // Helpdesk's current Dataverse choice only models SharePoint. This development adapter emulates
            // that contract while using an unmistakably local repository identifier.
            var id = new ExternalFileId("SharePoint", "development-local", "helpdesk", upload.FileId.ToString("D"));
            var logicalPath = upload.Scope.CorrelationId is null
                ? $"{upload.Scope.Scope}/{storedName}"
                : $"{upload.Scope.Scope}/{upload.Scope.CorrelationId}/{storedName}";
            var result = new StoredFile(id, info.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture),
                upload.OriginalName, storedName, upload.ContentType, info.Length, LogicalPath: logicalPath,
                Sha256: Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(), UploadedAt: upload.UploadedAt);
            await File.WriteAllTextAsync(metadataPath, JsonSerializer.Serialize(result), token);
            return result;
        }
        catch
        {
            File.Delete(path);
            File.Delete(metadataPath);
            throw;
        }
    }

    public async Task<FileDownload> DownloadAsync(ExternalFileId file, CancellationToken token)
    {
        var metadata = await GetMetadataAsync(file, token);
        return new(metadata, new FileStream(Path.Combine(root, metadata.StoredName), FileMode.Open, FileAccess.Read, FileShare.Read));
    }
    public async Task<StoredFile> GetMetadataAsync(ExternalFileId file, CancellationToken token)
    {
        var id = LocalId(file);
        var metadataPath = MetadataPath(id);
        if (!File.Exists(metadataPath)) throw new FileStorageException(FileStorageError.FileNotFound);
        var value = JsonSerializer.Deserialize<StoredFile>(await File.ReadAllTextAsync(metadataPath, token))
            ?? throw new FileStorageException(FileStorageError.FileNotFound);
        if (!File.Exists(Path.Combine(root, value.StoredName))) throw new FileStorageException(FileStorageError.FileNotFound);
        return value;
    }
    public async Task<bool> ExistsAsync(ExternalFileId file, CancellationToken token) { try { await GetMetadataAsync(file, token); return true; } catch (FileStorageException) { return false; } }
    public async Task<IReadOnlyList<StoredFile>> ListAsync(LogicalFileScope scope, CancellationToken token)
    {
        FileUploadRules.ValidateScope(scope);
        if (!Directory.Exists(MetadataRoot)) return [];
        var files = new List<StoredFile>();
        foreach (var path in Directory.EnumerateFiles(MetadataRoot, "*.json"))
        {
            var value = JsonSerializer.Deserialize<StoredFile>(await File.ReadAllTextAsync(path, token));
            if (value?.LogicalPath?.StartsWith(scope.Scope + "/", StringComparison.OrdinalIgnoreCase) == true)
                files.Add(value);
        }
        return files;
    }
    public Task<ExternalFolder> EnsureFolderAsync(LogicalFileScope scope, CancellationToken token) { FileUploadRules.ValidateScope(scope); Directory.CreateDirectory(root); return Task.FromResult(new ExternalFolder("SharePoint", "development-local", "helpdesk", "root", scope.Scope)); }
    public Task<FileStorageAvailability> CheckAvailabilityAsync(CancellationToken token) => Task.FromResult(new FileStorageAvailability(StorageAccessStatus.Available, StorageAccessStatus.Available, StorageAccessStatus.Available, StorageAccessStatus.Available, StorageAccessStatus.Available, StorageAccessStatus.Available));
    public async Task DeletePhysicallyAsync(PhysicalFileDeletion deletion, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(deletion.Reason)) throw new FileStorageException(FileStorageError.FileUnauthorized);
        var metadata = await GetMetadataAsync(deletion.Id, token);
        if (!string.Equals(metadata.ETag, deletion.ExpectedETag, StringComparison.Ordinal))
            throw new FileStorageException(FileStorageError.VersionConflict);
        File.Delete(Path.Combine(root, metadata.StoredName));
        File.Delete(MetadataPath(LocalId(deletion.Id)));
    }

    public Task<FileStorageDiagnosticReport> CheckAsync(CancellationToken token) => Task.FromResult(new FileStorageDiagnosticReport(
        Configuration: "Available", Authentication: "Available", Repository: "Available", Container: "Available",
        RootFolder: "Available", Read: "Available", Write: "Available"));

    private string MetadataPath(Guid id) => Path.Combine(MetadataRoot, id.ToString("N") + ".json");
    private static Guid LocalId(ExternalFileId file)
    {
        if (!string.Equals(file.RepositoryId, "development-local", StringComparison.Ordinal)
            || !string.Equals(file.ContainerId, "helpdesk", StringComparison.Ordinal)
            || !Guid.TryParse(file.FileId, out var id))
            throw new FileStorageException(FileStorageError.FileUnauthorized);
        return id;
    }
}
