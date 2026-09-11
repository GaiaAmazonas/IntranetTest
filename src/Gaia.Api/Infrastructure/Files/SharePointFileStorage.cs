using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Gaia.BuildingBlocks.Files;

namespace Gaia.Api.Infrastructure.Files;

internal sealed class SharePointFileStorage(SharePointStorageConfiguration options, GraphFileTransport transport,
    IEnumerable<IFileContentScanner> scanners, ILogger<SharePointFileStorage> logger) : IFileStorage, IFileStorageMaintenance
{
    internal const string Provider = "SharePoint";
    private const string ItemSelect = "id,name,eTag,size,webUrl,file,folder,parentReference,remoteItem";
    private static readonly Action<ILogger, string, Exception?> LogCleanupFailure = LoggerMessage.Define<string>(
        LogLevel.Warning, new EventId(4301, "UploadSessionCleanupFailed"), "File storage session cleanup failed: {Code}");
    private string Drive => $"drives/{Escape(options.DriveId)}";
    private static string Escape(string value) => Uri.EscapeDataString(value);
    private static Uri Graph(string path) => new("https://graph.microsoft.com/v1.0/" + path);
    private static string EncodePath(string path) => string.Join('/', path.Split('/').Select(Escape));

    public async Task<StoredFile> UploadAsync(FileUpload upload, Stream content, CancellationToken cancellationToken)
    {
        RequireEnabled();
        var storedName = FileUploadRules.ValidateAndCreateStoredName(upload, options.MaximumFileBytes, options.AllowedExtensions, options.AllowedMimeTypes);
        await using var spool = await FileTransferStreams.PrepareAsync(content, upload.Length, cancellationToken);
        var sha256 = Convert.ToHexString(await SHA256.HashDataAsync(spool, cancellationToken)).ToLowerInvariant();
        spool.Position = 0;
        var scanner = scanners.SingleOrDefault();
        if (scanner is null && options.RequireContentScan) throw new FileStorageException(FileStorageError.ScanUnavailable);
        if (scanner is not null)
        {
            var scan = await scanner.ScanAsync(upload, new BorrowedStream(spool), cancellationToken);
            if (scan.Status != FileScanStatus.Clean) throw new FileStorageException(scan.Status == FileScanStatus.Rejected
                ? FileStorageError.ScanRejected : FileStorageError.ScanUnavailable);
            spool.Position = 0;
        }
        var folder = await EnsureFolderAsync(upload.Scope, cancellationToken);
        var path = $"{Drive}/items/{Escape(folder.FolderId)}:/{Escape(storedName)}:";
        JsonElement item;
        if (upload.Length <= options.SimpleUploadThresholdBytes)
        {
            using var response = await transport.SendAsync(() =>
            {
                spool.Position = 0;
                var request = new HttpRequestMessage(HttpMethod.Put, Graph(path + "/content?@microsoft.graph.conflictBehavior=fail"));
                request.Content = new StreamContent(new BorrowedStream(spool));
                request.Content.Headers.ContentType = new MediaTypeHeaderValue(upload.ContentType);
                request.Content.Headers.ContentLength = upload.Length;
                return request;
            }, authenticated: true, retry: true, cancellationToken);
            GraphFileTransport.EnsureSuccess(response);
            item = await GraphFileTransport.JsonAsync(response, cancellationToken);
        }
        else item = await UploadSessionAsync(path, folder.FolderId, storedName, spool, upload.Length, cancellationToken);
        var result = ToMetadata(item, upload.OriginalName, upload.ContentType, folder.LogicalPath + "/" + storedName)
            with { Sha256 = sha256, UploadedAt = upload.UploadedAt };
        if (result.Length != upload.Length || result.StoredName != storedName)
            throw new FileStorageException(FileStorageError.VersionConflict);
        return result;
    }

    public async Task<ExternalFolder> EnsureFolderAsync(LogicalFileScope scope, CancellationToken cancellationToken)
    {
        RequireEnabled(); FileUploadRules.ValidateScope(scope);
        await ValidateRepositoryAsync(cancellationToken);
        var parent = await ReadAsync($"{Drive}/root?$select={ItemSelect}", cancellationToken);
        var logical = options.RootFolder + "/" + scope.Scope
            + (scope.CorrelationId is null ? "" : "/" + scope.CorrelationId);
        foreach (var segment in logical.Split('/'))
        {
            var path = $"{Drive}/items/{Escape(Text(parent, "id"))}:/{Escape(segment)}";
            var next = await TryReadAsync(path + "?$select=" + ItemSelect, cancellationToken);
            if (next is null)
            {
                using var response = await transport.SendAsync(() => new(HttpMethod.Post, Graph($"{Drive}/items/{Escape(Text(parent, "id"))}/children"))
                { Content = JsonContent.Create(new Dictionary<string, object> { ["name"] = segment, ["folder"] = new { }, ["@microsoft.graph.conflictBehavior"] = "fail" }) },
                    authenticated: true, retry: false, cancellationToken);
                if (response.StatusCode == HttpStatusCode.Conflict) next = await ReadAsync(path + "?$select=" + ItemSelect, cancellationToken);
                else { GraphFileTransport.EnsureSuccess(response); next = await GraphFileTransport.JsonAsync(response, cancellationToken); }
            }
            RequireFolder(next.Value);
            parent = next.Value;
        }
        return new(Provider, options.SiteId, options.DriveId, Text(parent, "id"), logical);
    }

    public async Task<StoredFile> GetMetadataAsync(ExternalFileId file, CancellationToken cancellationToken)
    {
        var item = await AuthorizedItemAsync(file, cancellationToken);
        // The business stores OriginalName from UploadAsync; Graph only knows the technical name.
        return ToMetadata(item, originalName: null);
    }

    public async Task<bool> ExistsAsync(ExternalFileId file, CancellationToken cancellationToken)
    {
        try { _ = await AuthorizedItemAsync(file, cancellationToken); return true; }
        catch (FileStorageException error) when (error.Code == FileStorageError.FileNotFound) { return false; }
    }

    public async Task<IReadOnlyList<StoredFile>> ListAsync(LogicalFileScope scope, CancellationToken cancellationToken)
    {
        RequireEnabled(); FileUploadRules.ValidateScope(scope);
        await ValidateRepositoryAsync(cancellationToken);
        var logical = options.RootFolder + "/" + scope.Scope
            + (scope.CorrelationId is null ? "" : "/" + scope.CorrelationId);
        var folder = await TryReadAsync($"{Drive}/root:/{EncodePath(logical)}?$select={ItemSelect}", cancellationToken);
        if (folder is null) return [];
        RequireFolder(folder.Value);
        var result = new List<StoredFile>();
        Uri? page = Graph($"{Drive}/items/{Escape(Text(folder.Value, "id"))}/children?$select={ItemSelect}");
        while (page is not null)
        {
            transport.ValidateDestination(page, true);
            using var response = await transport.SendAsync(() => new(HttpMethod.Get, page), true, true, cancellationToken);
            GraphFileTransport.EnsureSuccess(response);
            var json = await GraphFileTransport.JsonAsync(response, cancellationToken);
            foreach (var item in json.GetProperty("value").EnumerateArray())
                if (item.TryGetProperty("file", out _) && !item.TryGetProperty("remoteItem", out _))
                    result.Add(ToMetadata(item, originalName: null, logicalPath: logical + "/" + Text(item, "name")));
            page = json.TryGetProperty("@odata.nextLink", out var next)
                && Uri.TryCreate(next.GetString(), UriKind.Absolute, out var nextUri) ? nextUri : null;
        }
        return result;
    }

    public async Task<FileDownload> DownloadAsync(ExternalFileId file, CancellationToken cancellationToken)
    {
        var metadata = await GetMetadataAsync(file, cancellationToken);
        var uri = Graph($"{Drive}/items/{Escape(file.FileId)}/content");
        var authenticated = true;
        for (var redirect = 0; redirect < 4; redirect++)
        {
            var response = await transport.SendAsync(() => new(HttpMethod.Get, uri), authenticated, retry: true, cancellationToken);
            if (response.StatusCode is HttpStatusCode.Found or HttpStatusCode.Moved or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect)
            {
                using (response)
                {
                    var location = response.Headers.Location;
                    if (location is null || !location.IsAbsoluteUri) throw new FileStorageException(FileStorageError.FileUnauthorized);
                    transport.ValidateDestination(location, authenticated: false);
                    uri = location; authenticated = false;
                }
                continue;
            }
            try
            {
                GraphFileTransport.EnsureSuccess(response);
                return new(metadata, new ResponseOwnedStream(await response.Content.ReadAsStreamAsync(cancellationToken), response));
            }
            catch { response.Dispose(); throw; }
        }
        throw new FileStorageException(FileStorageError.TransientFailure);
    }

    public async Task DeletePhysicallyAsync(PhysicalFileDeletion deletion, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(deletion);
        if (string.IsNullOrWhiteSpace(deletion.Reason) || !EntityTagHeaderValue.TryParse(deletion.ExpectedETag, out var tag) || tag.Tag == "*")
            throw new FileStorageException(FileStorageError.VersionConflict);
        var item = await AuthorizedItemAsync(deletion.Id, cancellationToken);
        if (Text(item, "eTag") != deletion.ExpectedETag) throw new FileStorageException(FileStorageError.VersionConflict);
        using var response = await transport.SendAsync(() =>
        {
            var request = new HttpRequestMessage(HttpMethod.Delete, Graph($"{Drive}/items/{Escape(deletion.Id.FileId)}"));
            request.Headers.IfMatch.Add(tag); return request;
        }, authenticated: true, retry: false, cancellationToken);
        GraphFileTransport.EnsureSuccess(response);
    }

    public async Task<FileStorageAvailability> CheckAvailabilityAsync(CancellationToken cancellationToken)
    {
        RequireEnabled();
        await ValidateRepositoryAsync(cancellationToken);
        var root = await TryReadAsync($"{Drive}/root:/{EncodePath(options.RootFolder)}?$select={ItemSelect}", cancellationToken);
        if (root is not null) RequireFolder(root.Value);
        return new(StorageAccessStatus.Available, StorageAccessStatus.Available, StorageAccessStatus.Available,
            root is null ? StorageAccessStatus.Unavailable : StorageAccessStatus.Available,
            StorageAccessStatus.Available, StorageAccessStatus.Unknown);
    }

    private async Task ValidateRepositoryAsync(CancellationToken cancellationToken)
    {
        await ReadAsync($"sites/{Escape(options.SiteId)}?$select=id", cancellationToken, FileStorageError.RepositoryInaccessible);
        var uri = Graph($"sites/{Escape(options.SiteId)}/drives?$select=id");
        var visited = new HashSet<string>(StringComparer.Ordinal);
        while (visited.Add(uri.AbsoluteUri))
        {
            using var response = await transport.SendAsync(() => new(HttpMethod.Get, uri), true, true, cancellationToken);
            GraphFileTransport.EnsureSuccess(response, FileStorageError.ContainerInaccessible);
            var page = await GraphFileTransport.JsonAsync(response, cancellationToken);
            if (page.GetProperty("value").EnumerateArray().Any(item => Text(item, "id") == options.DriveId)) return;
            if (!page.TryGetProperty("@odata.nextLink", out var next)) break;
            if (!Uri.TryCreate(next.GetString(), UriKind.Absolute, out uri!)) throw new FileStorageException(FileStorageError.ContainerInaccessible);
        }
        throw new FileStorageException(FileStorageError.ContainerInaccessible);
    }

    private async Task<JsonElement> AuthorizedItemAsync(ExternalFileId id, CancellationToken cancellationToken)
    {
        RequireEnabled(); ArgumentNullException.ThrowIfNull(id);
        if (id.Provider != Provider || id.RepositoryId != options.SiteId || id.ContainerId != options.DriveId || string.IsNullOrWhiteSpace(id.FileId))
            throw new FileStorageException(FileStorageError.FileUnauthorized);
        await ValidateRepositoryAsync(cancellationToken);
        var root = await ReadAsync($"{Drive}/root:/{EncodePath(options.RootFolder)}?$select={ItemSelect}", cancellationToken);
        RequireFolder(root);
        var item = await ReadAsync($"{Drive}/items/{Escape(id.FileId)}?$select={ItemSelect}", cancellationToken);
        if (!item.TryGetProperty("file", out _) || item.TryGetProperty("remoteItem", out _)) throw new FileStorageException(FileStorageError.FileUnauthorized);
        var cursor = item;
        var visited = new HashSet<string>(StringComparer.Ordinal);
        for (var depth = 0; depth < 64; depth++)
        {
            if (!cursor.TryGetProperty("parentReference", out var parent) || Text(parent, "driveId") != options.DriveId)
                break;
            var parentId = Text(parent, "id");
            if (parentId == Text(root, "id")) return item;
            if (!visited.Add(parentId)) break;
            cursor = await ReadAsync($"{Drive}/items/{Escape(parentId)}?$select={ItemSelect}", cancellationToken);
            RequireFolder(cursor);
        }
        throw new FileStorageException(FileStorageError.FileUnauthorized);
    }

    private static void RequireFolder(JsonElement item)
    {
        if (!item.TryGetProperty("folder", out _) || item.TryGetProperty("remoteItem", out _))
            throw new FileStorageException(FileStorageError.FileUnauthorized);
    }

    private async Task<JsonElement> ReadAsync(string path, CancellationToken cancellationToken, FileStorageError? error = null)
    {
        using var response = await transport.SendAsync(() => new(HttpMethod.Get, Graph(path)), true, true, cancellationToken);
        GraphFileTransport.EnsureSuccess(response, error);
        return await GraphFileTransport.JsonAsync(response, cancellationToken);
    }

    private async Task<JsonElement?> TryReadAsync(string path, CancellationToken cancellationToken)
    {
        using var response = await transport.SendAsync(() => new(HttpMethod.Get, Graph(path)), true, true, cancellationToken);
        if (response.StatusCode == HttpStatusCode.NotFound) return null;
        GraphFileTransport.EnsureSuccess(response);
        return await GraphFileTransport.JsonAsync(response, cancellationToken);
    }

    private StoredFile ToMetadata(JsonElement item, string? originalName, string? mime = null, string? logicalPath = null) =>
        new(new(Provider, options.SiteId, options.DriveId, Text(item, "id")), Text(item, "eTag"), originalName,
            Text(item, "name"), mime ?? (item.TryGetProperty("file", out var file) && file.TryGetProperty("mimeType", out var type)
                ? type.GetString() ?? "application/octet-stream" : "application/octet-stream"), item.GetProperty("size").GetInt64(),
            WebUrl: item.TryGetProperty("webUrl", out var webUrl) && Uri.TryCreate(webUrl.GetString(), UriKind.Absolute, out var uri) ? uri : null,
            LogicalPath: logicalPath);

    private static string Text(JsonElement item, string property) => item.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } text
            ? text : throw new FileStorageException(FileStorageError.TransientFailure);

    private void RequireEnabled()
    {
        if (!options.Enabled) throw new FileStorageException(FileStorageError.MissingConfiguration);
    }

    private async Task<JsonElement> UploadSessionAsync(string path, string parentId, string name, FileStream spool, long length, CancellationToken cancellationToken)
    {
        using var created = await transport.SendAsync(() => new(HttpMethod.Post, Graph(path + "/createUploadSession"))
        { Content = JsonContent.Create(new { item = new Dictionary<string, object> { ["name"] = name, ["@microsoft.graph.conflictBehavior"] = "fail" } }) }, true, false, cancellationToken);
        GraphFileTransport.EnsureSuccess(created);
        var session = await GraphFileTransport.JsonAsync(created, cancellationToken);
        if (!Uri.TryCreate(Text(session, "uploadUrl"), UriKind.Absolute, out var uploadUri)) throw new FileStorageException(FileStorageError.TransientFailure);
        transport.ValidateDestination(uploadUri, false);
        var complete = false;
        try
        {
            var buffer = new byte[options.UploadChunkBytes];
            long offset = 0;
            var failures = 0;
            while (offset < length)
            {
                cancellationToken.ThrowIfCancellationRequested();
                spool.Position = offset;
                var count = (int)Math.Min(buffer.Length, length - offset);
                await spool.ReadExactlyAsync(buffer.AsMemory(0, count), cancellationToken);
                HttpResponseMessage? response = null;
                try
                {
                    response = await transport.SendAsync(() =>
                    {
                        var request = new HttpRequestMessage(HttpMethod.Put, uploadUri) { Content = new ByteArrayContent(buffer, 0, count) };
                        request.Content.Headers.ContentRange = new ContentRangeHeaderValue(offset, offset + count - 1, length);
                        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
                        return request;
                    }, false, false, cancellationToken);
                    if (response.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK)
                    { var result = await GraphFileTransport.JsonAsync(response, cancellationToken); complete = true; return result; }
                    if (response.StatusCode == HttpStatusCode.Accepted)
                    {
                        var next = ExpectedOffset(await GraphFileTransport.JsonAsync(response, cancellationToken), length);
                        if (next <= offset && ++failures > GraphFileTransport.MaximumRetries) throw new FileStorageException(FileStorageError.TransientFailure);
                        offset = next; continue;
                    }
                    if (!GraphFileTransport.IsTransient(response.StatusCode) && response.StatusCode != HttpStatusCode.RequestedRangeNotSatisfiable)
                        GraphFileTransport.EnsureSuccess(response);
                    if (++failures > GraphFileTransport.MaximumRetries) throw new FileStorageException(FileStorageError.TransientFailure);
                    await transport.DelayAsync(response.Headers.RetryAfter, failures - 1, cancellationToken);
                }
                catch (FileStorageException error) when (error.Code == FileStorageError.TransientFailure && response is null)
                {
                    if (++failures > GraphFileTransport.MaximumRetries) throw;
                    await transport.DelayAsync(null, failures - 1, cancellationToken);
                }
                finally { response?.Dispose(); }

                using var status = await transport.SendAsync(() => new(HttpMethod.Get, uploadUri), false, true, cancellationToken);
                if (status.StatusCode == HttpStatusCode.NotFound)
                {
                    // Last PUT may have committed while its response was lost. Never restart/overwrite blindly.
                    var committed = await TryReadAsync($"{Drive}/items/{Escape(parentId)}:/{Escape(name)}?$select={ItemSelect}", cancellationToken);
                    if (committed is not null && committed.Value.GetProperty("size").GetInt64() == length)
                    { complete = true; return committed.Value; }
                }
                GraphFileTransport.EnsureSuccess(status);
                offset = ExpectedOffset(await GraphFileTransport.JsonAsync(status, cancellationToken), length);
            }
            throw new FileStorageException(FileStorageError.TransientFailure);
        }
        finally
        {
            if (!complete)
            {
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try
                {
                    using var response = await transport.SendAsync(() => new(HttpMethod.Delete, uploadUri), false, false, cleanup.Token);
                    if (!response.IsSuccessStatusCode && response.StatusCode != HttpStatusCode.NotFound)
                        LogCleanupFailure(logger, "ProviderFailure", null);
                }
                catch (Exception error) when (error is FileStorageException or OperationCanceledException)
                { LogCleanupFailure(logger, "CleanupUnavailable", null); }
            }
        }
    }

    private static long ExpectedOffset(JsonElement session, long length)
    {
        if (!session.TryGetProperty("nextExpectedRanges", out var ranges) || ranges.ValueKind != JsonValueKind.Array)
            throw new FileStorageException(FileStorageError.TransientFailure);
        var starts = new List<long>();
        foreach (var range in ranges.EnumerateArray())
        {
            if (!long.TryParse(range.GetString()?.Split('-')[0], NumberStyles.None, CultureInfo.InvariantCulture, out var value)
                || value < 0 || value >= length) throw new FileStorageException(FileStorageError.TransientFailure);
            starts.Add(value);
        }
        return starts.Count > 0 ? starts.Min() : throw new FileStorageException(FileStorageError.TransientFailure);
    }
}
