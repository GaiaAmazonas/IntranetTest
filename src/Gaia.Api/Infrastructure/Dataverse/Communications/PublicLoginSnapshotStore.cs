using System.Text.Json;
using Gaia.Api.Infrastructure.Files;
using Gaia.BuildingBlocks.Files;
using Gaia.Modules.Communications;

namespace Gaia.Api.Infrastructure.Dataverse.Communications;

// The anonymous login never queries Dataverse. An authenticated publication creates this
// projection; image bytes remain in SharePoint and are read with the approved Graph identity.
internal sealed class PublicLoginSnapshotStore(
    ISharePointRepositoryReader repositories,
    IConfiguration configuration,
    IHostEnvironment environment,
    IHttpClientFactory clients,
    IEnumerable<IFileContentScanner> scanners,
    ILogger<SharePointFileStorage> storageLogger)
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private readonly string path = Path.Combine(environment.ContentRootPath, "App_Data", "public-login", "current.json");

    public async Task PublishAsync(PublicLoginConfigurationDto content,
        IReadOnlyDictionary<string, ExternalFileId> images, CancellationToken token)
    {
        if (!images.TryGetValue("desktop", out var desktop))
            throw new InvalidOperationException("La publicación requiere una imagen de escritorio.");
        if (images.Values.Any(image => image.Provider != desktop.Provider
            || image.RepositoryId != desktop.RepositoryId || image.ContainerId != desktop.ContainerId))
            throw new InvalidOperationException("Las imágenes publicadas deben pertenecer al mismo repositorio de SharePoint.");

        // This read occurs only inside the authenticated publish operation and therefore uses
        // the delegated Dataverse client. The resulting snapshot contains no credential material.
        var repository = await repositories.ReadAsync(desktop, token);
        var snapshot = new PublicLoginSnapshot(1, DateTimeOffset.UtcNow, content,
            images.ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase), repository);
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var temporary = Path.Combine(directory, $".{Guid.NewGuid():N}.tmp");
        try
        {
            await using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                81920, FileOptions.Asynchronous | FileOptions.WriteThrough))
                await JsonSerializer.SerializeAsync(stream, snapshot, Json, token);
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    public async Task<PublicLoginConfigurationDto?> ReadAsync(CancellationToken token) =>
        (await SnapshotAsync(token))?.Content;

    public async Task<MediaContent?> ReadImageAsync(string variant, CancellationToken token)
    {
        if (variant is not ("desktop" or "tablet" or "mobile")) return null;
        var snapshot = await SnapshotAsync(token);
        if (snapshot is null) return null;
        var key = variant;
        if (!snapshot.Images.ContainsKey(key)) key = variant == "mobile" && snapshot.Images.ContainsKey("tablet") ? "tablet" : "desktop";
        if (!snapshot.Images.TryGetValue(key, out var id)) return null;

        var (options, prefix) = RepositoryStorageConfiguration.Build(snapshot.Repository, configuration, environment.EnvironmentName);
        using var tokens = GraphApplicationTokenProvider.Create(options, configuration, prefix);
        using var transport = new GraphFileTransport(clients.CreateClient("GraphFiles"), tokens, options, TimeProvider.System);
        var storage = new SharePointFileStorage(options, transport, scanners, storageLogger);
        await using var download = await storage.DownloadAsync(id, token);
        using var memory = new MemoryStream();
        await download.Content.CopyToAsync(memory, token);
        return new(memory.ToArray(), download.Metadata.ContentType);
    }

    private async Task<PublicLoginSnapshot?> SnapshotAsync(CancellationToken token)
    {
        if (!File.Exists(path)) return null;
        try
        {
            await using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 81920,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var snapshot = await JsonSerializer.DeserializeAsync<PublicLoginSnapshot>(stream, Json, token);
            return snapshot?.Version == 1 ? snapshot : null;
        }
        catch (JsonException) { return null; }
        catch (IOException) { return null; }
    }

    private sealed record PublicLoginSnapshot(int Version, DateTimeOffset PublishedAt,
        PublicLoginConfigurationDto Content, Dictionary<string, ExternalFileId> Images,
        SharePointRepositorySettings Repository);
}
