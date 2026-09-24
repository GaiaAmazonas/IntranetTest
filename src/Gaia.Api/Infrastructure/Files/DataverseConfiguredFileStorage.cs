using Gaia.BuildingBlocks.Files;

namespace Gaia.Api.Infrastructure.Files;

// Request-scoped ownership: each operation pins a configuration snapshot until its transfer completes.
// No settings or delegated Dataverse results are cached globally across users.
internal sealed class DataverseConfiguredFileStorage(ISharePointRepositoryReader reader, IConfiguration configuration,
    IHostEnvironment environment, IHttpClientFactory clients, IEnumerable<IFileContentScanner> scanners,
    ILogger<SharePointFileStorage> logger) : IFileStorage, IFileStorageMaintenance, IFileStorageDiagnostics, IDisposable
{
    private static readonly Action<ILogger, FileStorageError, Exception?> LogRepositoryProfileFailure =
        LoggerMessage.Define<FileStorageError>(LogLevel.Warning, new EventId(4302, "RepositoryProfileValidationFailed"),
            "SharePoint storage configuration validation failed at repository profile mapping: {Code}");
    private static readonly Action<ILogger, FileStorageError, Exception?> LogCredentialFailure =
        LoggerMessage.Define<FileStorageError>(LogLevel.Warning, new EventId(4303, "StorageCredentialLoadingFailed"),
            "SharePoint storage configuration validation failed at credential loading: {Code}");
    private readonly List<(GraphFileTransport Transport, GraphApplicationTokenProvider Tokens)> resources = [];

    public void Dispose()
    {
        lock (resources)
        {
            foreach (var resource in resources) { resource.Transport.Dispose(); resource.Tokens.Dispose(); }
            resources.Clear();
        }
    }

    internal static void ValidateState(int state, bool write, bool diagnostic = false)
    {
        if (write ? state != 2 : diagnostic ? state is not (1 or 2 or 3) : state is not (2 or 3))
            throw new FileStorageException(FileStorageError.FileUnauthorized);
    }

    private async Task<(SharePointFileStorage Storage, FileStorageDiagnostics Diagnostics)> ResolveAsync(
        ExternalFileId? file, bool write, CancellationToken token, bool diagnostic = false)
    {
        if (!configuration.GetValue("FileStorage:SharePoint:Enabled", false))
            throw new FileStorageException(FileStorageError.MissingConfiguration);
        var row = await reader.ReadAsync(file, token);
        ValidateState(row.OperationalState, write, diagnostic);
        SharePointStorageConfiguration options;
        string prefix;
        try { (options, prefix) = RepositoryStorageConfiguration.Build(row, configuration, environment.EnvironmentName); }
        catch (FileStorageException error)
        {
            LogRepositoryProfileFailure(logger, error.Code, null);
            throw;
        }
        GraphApplicationTokenProvider credentials;
        try { credentials = GraphApplicationTokenProvider.Create(options, configuration, prefix); }
        catch (FileStorageException error)
        {
            LogCredentialFailure(logger, error.Code, null);
            throw;
        }
        GraphFileTransport transport;
        try { transport = new(clients.CreateClient("GraphFiles"), credentials, options, TimeProvider.System); }
        catch { credentials.Dispose(); throw; }
        lock (resources) resources.Add((transport, credentials));
        return (new(options, transport, scanners, logger), new(options, transport));
    }

    public async Task<StoredFile> UploadAsync(FileUpload upload, Stream content, CancellationToken cancellationToken) =>
        await (await ResolveAsync(null, true, cancellationToken)).Storage.UploadAsync(upload, content, cancellationToken);
    public async Task<ExternalFolder> EnsureFolderAsync(LogicalFileScope scope, CancellationToken cancellationToken) =>
        await (await ResolveAsync(null, true, cancellationToken)).Storage.EnsureFolderAsync(scope, cancellationToken);
    public async Task<StoredFile> GetMetadataAsync(ExternalFileId file, CancellationToken cancellationToken) =>
        await (await ResolveAsync(file, false, cancellationToken)).Storage.GetMetadataAsync(file, cancellationToken);
    public async Task<bool> ExistsAsync(ExternalFileId file, CancellationToken cancellationToken) =>
        await (await ResolveAsync(file, false, cancellationToken)).Storage.ExistsAsync(file, cancellationToken);
    public async Task<IReadOnlyList<StoredFile>> ListAsync(LogicalFileScope scope, CancellationToken cancellationToken) =>
        await (await ResolveAsync(null, false, cancellationToken)).Storage.ListAsync(scope, cancellationToken);
    public async Task<FileDownload> DownloadAsync(ExternalFileId file, CancellationToken cancellationToken) =>
        await (await ResolveAsync(file, false, cancellationToken)).Storage.DownloadAsync(file, cancellationToken);
    public async Task DeletePhysicallyAsync(PhysicalFileDeletion deletion, CancellationToken cancellationToken) =>
        await (await ResolveAsync(deletion.Id, true, cancellationToken)).Storage.DeletePhysicallyAsync(deletion, cancellationToken);
    public async Task<FileStorageAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken) =>
        await (await ResolveAsync(null, false, cancellationToken, true)).Storage.CheckAvailabilityAsync(cancellationToken);
    public async Task<FileStorageDiagnosticReport> CheckAsync(CancellationToken cancellationToken)
    {
        try { return await (await ResolveAsync(null, false, cancellationToken, true)).Diagnostics.CheckAsync(cancellationToken); }
        catch (FileStorageException error) { return new(Configuration: "Unavailable", ErrorCode: error.Code.ToString()); }
    }
}
