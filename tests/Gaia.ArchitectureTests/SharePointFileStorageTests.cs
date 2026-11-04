using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using Gaia.Api.Infrastructure.Files;
using Gaia.BuildingBlocks.Files;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace Gaia.ArchitectureTests;

public sealed class SharePointFileStorageTests
{
    private const string Site = "tenant.sharepoint.com,site,web";
    private const string Drive = "drive";
    private static readonly ExternalFileId FileId = new("SharePoint", Site, Drive, "file");
    private static readonly string[] InitialRange = ["0-"];
    private static readonly string[] SecondRange = ["327680-"];

    [Fact]
    public async Task SmallUploadUsesAppTokenAndTechnicalNameAndReturnsStableMetadata()
    {
        using var handler = new FakeGraph();
        FolderReplies(handler);
        handler.Add(async request =>
        {
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Equal("application/pdf", request.Content!.Headers.ContentType!.MediaType);
            Assert.Contains("conflictBehavior=fail", request.RequestUri!.Query, StringComparison.Ordinal);
            Assert.Equal("%PDF-", await request.Content.ReadAsStringAsync());
            var name = request.RequestUri.AbsolutePath.Split(":/")[1].Split(':')[0];
            Assert.DoesNotContain("report", name, StringComparison.Ordinal);
            return Item(name, 5);
        });
        using var transport = Transport(handler);
        var store = Store(transport);
        await using var content = new MemoryStream(PdfBytes(5));
        var result = await store.UploadAsync(Upload("correlation", "report.pdf", 5), content, default);
        Assert.Equal(FileId, result.Id);
        Assert.Equal("report.pdf", result.OriginalName);
        Assert.Equal("\"version\"", result.ETag);
        Assert.Null(result.WebUrl);
        Assert.StartsWith("Gaia/Files/Documents/correlation/", result.LogicalPath);
        Assert.All(handler.Requests, request => Assert.Equal("Bearer test-app-token", request.Authorization));
        Assert.True(content.CanRead);
        Assert.Empty(handler.Replies);
    }

    [Fact]
    public async Task SmallUploadRecoversWhenGraphCommittedBeforeReturningConflict()
    {
        using var handler = new FakeGraph();
        FolderReplies(handler);
        string? name = null;
        handler.Add(request =>
        {
            name = request.RequestUri!.AbsolutePath.Split(":/")[1].Split(':')[0];
            return new HttpResponseMessage(HttpStatusCode.Conflict);
        });
        handler.Add(_ => Item(name!, 5));
        using var transport = Transport(handler);
        await using var content = new MemoryStream(PdfBytes(5));

        var result = await Store(transport).UploadAsync(Upload("correlation", "report.pdf", 5), content, default);

        Assert.Equal(5, result.Length);
        Assert.Equal(name, result.StoredName);
        Assert.Empty(handler.Replies);
    }

    [Fact]
    public async Task SharePointOfficeNormalizationUsesProviderStoredLengthWithoutFalseConflict()
    {
        using var handler = new FakeGraph();
        FolderReplies(handler);
        handler.Add(request =>
        {
            var name = request.RequestUri!.AbsolutePath.Split(":/")[1].Split(':')[0];
            return Item(name, 19);
        });
        using var transport = Transport(handler);
        var store = Store(transport);
        await using var content = new MemoryStream(OpenXmlBytes(12));
        var upload = new FileUpload(new("Documents","correlation"),"slides.pptx",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation",content.Length,
            Guid.NewGuid(),DateTimeOffset.UtcNow);

        var result = await store.UploadAsync(upload, content, default);

        Assert.Equal(19,result.Length);
        Assert.EndsWith(".pptx",result.StoredName,StringComparison.Ordinal);
        Assert.Empty(handler.Replies);
    }

    [Fact]
    public async Task WrongLengthAndRequiredScannerPreventAnyRemoteWrite()
    {
        using var handler = new FakeGraph();
        using var transport = Transport(handler);
        await using var source = new MemoryStream(PdfBytes(10));
        var error = await Assert.ThrowsAsync<FileStorageException>(() => Store(transport).UploadAsync(
            Upload("id", "report.pdf", 9), source, default));
        Assert.Equal(FileStorageError.InvalidLength, error.Code);
        source.Position = 0;
        var options = Options("RequireContentScan", "true");
        error = await Assert.ThrowsAsync<FileStorageException>(() => Store(transport, options).UploadAsync(
            Upload("id", "report.pdf", 10), source, default));
        Assert.Equal(FileStorageError.ScanUnavailable, error.Code);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task ScannerRejectionPreventsRemoteOperations()
    {
        using var handler = new FakeGraph();
        using var transport = Transport(handler);
        var store = new SharePointFileStorage(Options(), transport, [new RejectScanner()], NullLogger<SharePointFileStorage>.Instance);
        await using var source = new MemoryStream(PdfBytes(5));
        var error = await Assert.ThrowsAsync<FileStorageException>(() => store.UploadAsync(
            Upload("id", "report.pdf", 5), source, default));
        Assert.Equal(FileStorageError.ScanRejected, error.Code);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task SessionResumesAfterThrottlingWithoutLeakingBearer()
    {
        using var handler = new FakeGraph(); FolderReplies(handler);
        string? name = null;
        handler.Add(request =>
        {
            Assert.Null(request.Content);
            name = UploadSessionName(request);
            return Json(new { uploadUrl = "https://tenant.sharepoint.com/upload?signature=private" });
        });
        handler.Add(request =>
        {
            Assert.Equal("bytes 0-327679/327690", request.Content!.Headers.ContentRange!.ToString());
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new RetryConditionHeaderValue(TimeSpan.Zero); return response;
        });
        handler.Add(request => { Assert.Equal(HttpMethod.Get, request.Method); return Json(new { nextExpectedRanges = InitialRange }); });
        handler.Add(request =>
        {
            Assert.Equal("bytes 0-327679/327690", request.Content!.Headers.ContentRange!.ToString());
            return Json(new { nextExpectedRanges = SecondRange }, HttpStatusCode.Accepted);
        });
        handler.Add(request =>
        {
            Assert.Equal("bytes 327680-327689/327690", request.Content!.Headers.ContentRange!.ToString());
            return Item(name!, 327690);
        });
        using var transport = Transport(handler);
        await using var source = new MemoryStream(PdfBytes(327690));
        var result = await Store(transport).UploadAsync(Upload("id", "a.pdf", source.Length), source, default);
        Assert.Equal(327690, result.Length);
        Assert.All(handler.Requests.Where(request => request.Host != "graph.microsoft.com"), request => Assert.Null(request.Authorization));
        Assert.Empty(handler.Replies);
    }

    [Fact]
    public async Task SessionFailureCancelsSessionAndDoesNotRetryForbidden()
    {
        using var handler = new FakeGraph(); FolderReplies(handler);
        handler.Add(_ => Json(new { uploadUrl = "https://tenant.sharepoint.com/upload" }));
        handler.Add(_ => new HttpResponseMessage(HttpStatusCode.Forbidden));
        handler.Add(request => { Assert.Equal(HttpMethod.Delete, request.Method); return new HttpResponseMessage(HttpStatusCode.NoContent); });
        using var transport = Transport(handler);
        await using var source = new MemoryStream(PdfBytes(101));
        var error = await Assert.ThrowsAsync<FileStorageException>(() => Store(transport).UploadAsync(
            Upload("id", "a.pdf", 101), source, default));
        Assert.Equal(FileStorageError.FileUnauthorized, error.Code);
        Assert.Empty(handler.Replies);
    }

    [Fact]
    public async Task SessionCreationOmitsOptionalBodyAndReportsSafeProviderCode()
    {
        using var handler = new FakeGraph(); FolderReplies(handler);
        handler.Add(request =>
        {
            Assert.Null(request.Content);
            return Json(new { error = new { code = "invalidRequest", message = "provider detail" } },
                HttpStatusCode.BadRequest);
        });
        using var transport = Transport(handler);
        await using var source = new MemoryStream(PdfBytes(101));

        var error = await Assert.ThrowsAsync<FileStorageException>(() => Store(transport).UploadAsync(
            Upload("id","a.pdf",101),source,default));

        Assert.Equal(FileStorageError.TransientFailure,error.Code);
        Assert.Equal("SP_SESSION_CREATE_INVALIDREQUEST_HTTP_400",error.DiagnosticCode);
        Assert.Empty(handler.Replies);
    }

    [Fact]
    public async Task SessionProtocolFailureReportsStageAndHttpStatus()
    {
        using var handler = new FakeGraph(); FolderReplies(handler);
        handler.Add(_ => Json(new { uploadUrl = "https://tenant.sharepoint.com/upload" }));
        handler.Add(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));
        handler.Add(request =>
        {
            Assert.Equal(HttpMethod.Delete, request.Method);
            return new HttpResponseMessage(HttpStatusCode.NoContent);
        });
        using var transport = Transport(handler);
        await using var source = new MemoryStream(PdfBytes(101));

        var error = await Assert.ThrowsAsync<FileStorageException>(() => Store(transport).UploadAsync(
            Upload("id", "a.pdf", 101), source, default));

        Assert.Equal(FileStorageError.TransientFailure,error.Code);
        Assert.Equal("SP_SESSION_CHUNK_HTTP_400",error.DiagnosticCode);
        Assert.Empty(handler.Replies);
    }

    [Fact]
    public async Task SessionRetryBudgetResetsAfterConfirmedForwardProgress()
    {
        using var handler = new FakeGraph(); FolderReplies(handler);
        string? name = null;
        handler.Add(request =>
        {
            Assert.Null(request.Content);
            name = UploadSessionName(request);
            return Json(new { uploadUrl = "https://tenant.sharepoint.com/upload" });
        });
        for (var attempt = 0; attempt < GraphFileTransport.MaximumRetries; attempt++)
        {
            handler.Add(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
            handler.Add(_ => Json(new { nextExpectedRanges = InitialRange }));
        }
        handler.Add(_ => Json(new { nextExpectedRanges = SecondRange },HttpStatusCode.Accepted));
        handler.Add(_ => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        handler.Add(_ => Json(new { nextExpectedRanges = SecondRange }));
        handler.Add(_ => Item(name!,327690));
        using var transport = Transport(handler);
        await using var source = new MemoryStream(PdfBytes(327690));

        var result = await Store(transport).UploadAsync(Upload("id","a.pdf",source.Length),source,default);

        Assert.Equal(327690,result.Length);
        Assert.Empty(handler.Replies);
    }

    [Fact]
    public async Task SessionUploadRecoversWhenFinalChunkWasCommittedBeforeConflictResponse()
    {
        using var handler = new FakeGraph(); FolderReplies(handler);
        string? name = null;
        handler.Add(request =>
        {
            Assert.Null(request.Content);
            name = UploadSessionName(request);
            return Json(new { uploadUrl = "https://tenant.sharepoint.com/upload?signature=private" });
        });
        handler.Add(_ => new HttpResponseMessage(HttpStatusCode.Conflict));
        handler.Add(_ => Item(name!, 101));
        using var transport = Transport(handler);
        await using var source = new MemoryStream(PdfBytes(101));

        var result = await Store(transport).UploadAsync(Upload("id", "a.pdf", source.Length), source, default);

        Assert.Equal(101, result.Length);
        Assert.Equal(name, result.StoredName);
        Assert.Empty(handler.Replies);
    }

    [Fact]
    public async Task DownloadValidatesAncestryAndOwnsResponseStream()
    {
        using var handler = new FakeGraph(); MetadataReplies(handler);
        handler.Add(_ =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.Found);
            response.Headers.Location = new("https://tenant.sharepoint.com/download?signature=private"); return response;
        });
        var stream = new MemoryStream(Encoding.UTF8.GetBytes("hello"));
        handler.Add(request => { Assert.Null(request.Headers.Authorization); return new(HttpStatusCode.OK) { Content = new StreamContent(stream) }; });
        using var transport = Transport(handler);
        await using (var download = await Store(transport).DownloadAsync(FileId, default))
        {
            using var reader = new StreamReader(download.Content, leaveOpen: true);
            Assert.Equal("hello", await reader.ReadToEndAsync());
            Assert.Null(download.Metadata.WebUrl);
            Assert.Null(download.Metadata.OriginalName);
        }
        Assert.False(stream.CanRead);
        Assert.Empty(handler.Replies);
    }

    [Fact]
    public async Task PreviewReturnsOnlyAValidatedShortLivedHttpsUrl()
    {
        using var handler = new FakeGraph(); MetadataReplies(handler);
        handler.Add(request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.EndsWith("/drives/drive/items/file/preview", request.RequestUri!.AbsolutePath, StringComparison.Ordinal);
            return Json(new { getUrl = "https://view.officeapps.live.com/op/embed.aspx?opaque=temporary" });
        });
        using var transport = Transport(handler);

        var preview = await Store(transport).CreatePreviewAsync(FileId, default);

        Assert.Equal("https", preview.Url.Scheme);
        Assert.Contains("opaque=temporary", preview.Url.Query, StringComparison.Ordinal);
        Assert.Empty(handler.Replies);
    }

    [Fact]
    public async Task ForeignRepositoryIsRejectedBeforeHttp()
    {
        using var handler = new FakeGraph(); using var transport = Transport(handler);
        var error = await Assert.ThrowsAsync<FileStorageException>(() => Store(transport).GetMetadataAsync(FileId with { RepositoryId = "other" }, default));
        Assert.Equal(FileStorageError.FileUnauthorized, error.Code); Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task FileOutsideRootIsRejected()
    {
        using var handler = new FakeGraph(); RepositoryReplies(handler);
        handler.Add(_ => Folder("root"));
        handler.Add(_ => Item("file.pdf", 5, "outside"));
        handler.Add(_ => Json(new { id = "outside", folder = new { } }));
        using var transport = Transport(handler);
        var error = await Assert.ThrowsAsync<FileStorageException>(() => Store(transport).GetMetadataAsync(FileId, default));
        Assert.Equal(FileStorageError.FileUnauthorized, error.Code);
    }

    [Fact]
    public async Task DeleteRequiresCurrentETagAndUsesIfMatch()
    {
        using var handler = new FakeGraph(); MetadataReplies(handler);
        handler.Add(request =>
        {
            Assert.Equal(HttpMethod.Delete, request.Method); Assert.Equal("\"version\"", request.Headers.IfMatch.Single().Tag);
            return new(HttpStatusCode.NoContent);
        });
        using var transport = Transport(handler);
        await Store(transport).DeletePhysicallyAsync(new(FileId, "\"version\"", "Controlled cleanup"), default);
        Assert.Empty(handler.Replies);
    }

    [Fact]
    public async Task StaleETagCannotDeleteFile()
    {
        using var handler = new FakeGraph(); MetadataReplies(handler); using var transport = Transport(handler);
        var error = await Assert.ThrowsAsync<FileStorageException>(() => Store(transport).DeletePhysicallyAsync(new(FileId, "\"old\"", "Cleanup"), default));
        Assert.Equal(FileStorageError.VersionConflict, error.Code);
        Assert.DoesNotContain(handler.Requests, request => request.Method == HttpMethod.Delete);
    }

    [Theory]
    [InlineData("http://tenant.sharepoint.com/file")]
    [InlineData("https://tenant.sharepoint.com.attacker.org/file")]
    [InlineData("https://127.0.0.1/file")]
    [InlineData("https://tenant.sharepoint.com:8443/file")]
    [InlineData("https://user@tenant.sharepoint.com/file")]
    public void TransferUrlsFailClosed(string address)
    {
        using var handler = new FakeGraph(); using var transport = Transport(handler);
        Assert.Throws<FileStorageException>(() => transport.ValidateDestination(new(address), false));
        Assert.Throws<FileStorageException>(() => transport.ValidateDestination(new(address), true));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, FileStorageError.InvalidCredentials)]
    [InlineData(HttpStatusCode.Forbidden, FileStorageError.FileUnauthorized)]
    [InlineData(HttpStatusCode.NotFound, FileStorageError.FileNotFound)]
    [InlineData(HttpStatusCode.Conflict, FileStorageError.VersionConflict)]
    public async Task PermanentErrorsAreNotRetriedOrExposed(HttpStatusCode status, FileStorageError code)
    {
        using var handler = new FakeGraph(); handler.Add(_ => new(status) { Content = new StringContent("secret-token-provider-body") });
        using var transport = Transport(handler);
        using var response = await transport.SendAsync(() => new(HttpMethod.Get, "https://graph.microsoft.com/v1.0/test"), true, true, default);
        var error = Assert.Throws<FileStorageException>(() => GraphFileTransport.EnsureSuccess(response));
        Assert.Equal(code, error.Code); Assert.DoesNotContain("secret-token", error.Message, StringComparison.Ordinal); Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ReadinessDoesNotWriteOrClaimWritePermission()
    {
        using var handler = new FakeGraph(); RepositoryReplies(handler); handler.Add(_ => new(HttpStatusCode.NotFound));
        using var transport = Transport(handler);
        var result = await Store(transport).CheckAvailabilityAsync(default);
        Assert.Equal(StorageAccessStatus.Unavailable, result.RootFolder);
        Assert.Equal(StorageAccessStatus.Unknown, result.Write);
        Assert.All(handler.Requests, request => Assert.Equal(HttpMethod.Get, request.Method));
    }

    [Fact]
    public async Task TokenProviderUsesApplicationScopeAndSanitizesFailures()
    {
        using var provider = new GraphApplicationTokenProvider(new FakeCredential(false));
        Assert.Equal("app", await provider.GetAsync(default));
        using var failing = new GraphApplicationTokenProvider(new FakeCredential(true));
        var error = await Assert.ThrowsAsync<FileStorageException>(async () => await failing.GetAsync(default));
        Assert.Equal(FileStorageError.InvalidCredentials, error.Code); Assert.Null(error.InnerException);
    }

    [Fact]
    public void SecretFromOrdinaryConfigurationIsRejected()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["FileStorage:SharePoint:ClientSecret"] = "not-external" }).Build();
        Assert.Throws<FileStorageException>(() => GraphApplicationTokenProvider.ExternalSecret(config, "ClientSecret", true));
    }

    [Fact]
    public async Task DisabledDiagnosticsDoNotContactGraph()
    {
        using var handler = new FakeGraph();
        using var transport = Transport(handler);
        var result = await new FileStorageDiagnostics(Options("Enabled", "false"), transport).CheckAsync(default);
        Assert.Equal("Unavailable", result.Configuration);
        Assert.Equal("Unknown", result.Write);
        Assert.Equal("MissingConfiguration", result.ErrorCode);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task DiagnosticsVerifyReadWithoutWriting()
    {
        using var handler = new FakeGraph(); RepositoryReplies(handler); handler.Add(_ => Folder("root"));
        using var transport = Transport(handler);
        var result = await new FileStorageDiagnostics(Options(), transport).CheckAsync(default);
        Assert.Equal("Available", result.Authentication);
        Assert.Equal("Available", result.Repository);
        Assert.Equal("Available", result.Container);
        Assert.Equal("Available", result.RootFolder);
        Assert.Equal("Available", result.Read);
        Assert.Equal("Unknown", result.Write);
        Assert.Null(result.ErrorCode);
        Assert.All(handler.Requests, request => Assert.Equal(HttpMethod.Get, request.Method));
        Assert.DoesNotContain(Site, JsonSerializer.Serialize(result), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "Unavailable", "Unknown", "InvalidCredentials")]
    [InlineData(HttpStatusCode.Forbidden, "Unknown", "Unavailable", "FileUnauthorized")]
    [InlineData(HttpStatusCode.NotFound, "Unknown", "Unavailable", "FileNotFound")]
    public async Task DiagnosticErrorsAreSanitizedAndDoNotRetryPermanentFailures(HttpStatusCode status,
        string authentication, string repository, string code)
    {
        using var handler = new FakeGraph();
        handler.Add(_ => new(status) { Content = new StringContent("sensitive-provider-response") });
        using var transport = Transport(handler);
        var result = await new FileStorageDiagnostics(Options(), transport).CheckAsync(default);
        Assert.Equal(authentication, result.Authentication); Assert.Equal(repository, result.Repository);
        Assert.Equal(code, result.ErrorCode); Assert.Equal("Unknown", result.Write);
        Assert.DoesNotContain("sensitive", JsonSerializer.Serialize(result), StringComparison.Ordinal);
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task DiagnosticMissingRootDoesNotCreateIt()
    {
        using var handler = new FakeGraph(); RepositoryReplies(handler); handler.Add(_ => new(HttpStatusCode.NotFound));
        using var transport = Transport(handler);
        var result = await new FileStorageDiagnostics(Options(), transport).CheckAsync(default);
        Assert.Equal("Available", result.Container); Assert.Equal("Unavailable", result.RootFolder);
        Assert.Equal("Unknown", result.Read); Assert.Equal("Unknown", result.Write);
        Assert.All(handler.Requests, request => Assert.Equal(HttpMethod.Get, request.Method));
    }

    [Fact]
    public async Task DiagnosticRejectsDriveOutsideConfiguredSite()
    {
        using var handler = new FakeGraph(); handler.Add(_ => Json(new { id = Site }));
        handler.Add(_ => Json(new { value = Array.Empty<object>() }));
        using var transport = Transport(handler);
        var result = await new FileStorageDiagnostics(Options(), transport).CheckAsync(default);
        Assert.Equal("Unavailable", result.Container); Assert.Equal("Unknown", result.RootFolder);
        Assert.Equal("ContainerInaccessible", result.ErrorCode); Assert.Equal(2, handler.Requests.Count);
    }

    [Fact]
    public async Task DiagnosticRejectsExternalPaginationBeforeSendingToken()
    {
        using var handler = new FakeGraph(); handler.Add(_ => Json(new { id = Site }));
        handler.Add(_ => Json(new Dictionary<string, object>
        { ["value"] = Array.Empty<object>(), ["@odata.nextLink"] = "https://external.example/steal" }));
        using var transport = Transport(handler);
        var result = await new FileStorageDiagnostics(Options(), transport).CheckAsync(default);
        Assert.Equal("FileUnauthorized", result.ErrorCode); Assert.Equal(2, handler.Requests.Count);
        Assert.All(handler.Requests, request => Assert.Equal("graph.microsoft.com", request.Host));
    }

    [Fact]
    public async Task DiagnosticPropagatesCallerCancellation()
    {
        using var handler = new FakeGraph();
        handler.Add(_ => Task.FromException<HttpResponseMessage>(new OperationCanceledException()));
        using var transport = Transport(handler);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            new FileStorageDiagnostics(Options(), transport).CheckAsync(cancellation.Token));
    }

    private static SharePointStorageConfiguration Options(string? key = null, string? value = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["Enabled"] = "true", ["TenantId"] = "11111111-1111-1111-1111-111111111111",
            ["ClientId"] = "22222222-2222-2222-2222-222222222222", ["AuthenticationMethod"] = "ManagedIdentity",
            ["SiteId"] = Site, ["DriveId"] = Drive, ["RootFolder"] = "Gaia/Files", ["MaximumFileBytes"] = "1000000",
            ["SimpleUploadThresholdBytes"] = "100", ["UploadChunkBytes"] = "327680",
            ["AllowedExtensions:0"] = ".pdf", ["AllowedMimeTypes:0"] = "application/pdf",
            ["AllowedExtensions:1"] = ".pptx",
            ["AllowedMimeTypes:1"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            ["TransferAllowedHosts:0"] = "tenant.sharepoint.com"
        };
        if (key is not null) values[key] = value;
        return SharePointStorageConfiguration.From(new ConfigurationBuilder().AddInMemoryCollection(
            values.ToDictionary(pair => "FileStorage:SharePoint:" + pair.Key, pair => pair.Value)).Build(), "Development");
    }
    private static GraphFileTransport Transport(FakeGraph handler) => new(new HttpClient(handler, disposeHandler: false), new FakeTokens(), Options(), TimeProvider.System);
    private static SharePointFileStorage Store(GraphFileTransport transport, SharePointStorageConfiguration? options = null) => new(options ?? Options(), transport, [], NullLogger<SharePointFileStorage>.Instance);
    private static FileUpload Upload(string correlation, string name, long length) =>
        new(new("Documents", correlation), name, "application/pdf", length, Guid.NewGuid(), DateTimeOffset.UtcNow);
    private static byte[] PdfBytes(int length)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(length, 5);
        var bytes = new byte[length];
        "%PDF-"u8.CopyTo(bytes);
        return bytes;
    }
    private static byte[] OpenXmlBytes(int length)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(length,4);
        var bytes=new byte[length];
        new byte[]{0x50,0x4b,0x03,0x04}.CopyTo(bytes,0);
        return bytes;
    }
    private static string UploadSessionName(HttpRequestMessage request)
    {
        var path=request.RequestUri!.AbsolutePath;
        var marker=path.IndexOf(":/",StringComparison.Ordinal);
        var end=path.IndexOf(":/createUploadSession",marker+2,StringComparison.Ordinal);
        Assert.True(marker>=0&&end>marker);
        return Uri.UnescapeDataString(path[(marker+2)..end]);
    }
    private static void RepositoryReplies(FakeGraph handler)
    { handler.Add(_ => Json(new { id = Site })); handler.Add(_ => Json(new { value = new[] { new { id = Drive } } })); }
    private static void FolderReplies(FakeGraph handler)
    {
        RepositoryReplies(handler);
        foreach (var id in new[] { "drive-root", "gaia", "root", "scope", "correlation" }) handler.Add(_ => Folder(id));
    }
    private static void MetadataReplies(FakeGraph handler)
    { RepositoryReplies(handler); handler.Add(_ => Folder("root")); handler.Add(_ => Item("technical.pdf", 5, "child")); handler.Add(_ => Folder("child", "root")); }
    private static HttpResponseMessage Folder(string id, string? parentId = null) => Json(new { id, folder = new { }, parentReference = new { id = parentId, driveId = Drive } });
    private static HttpResponseMessage Item(string name, long size, string parentId = "root") => Json(new { id = "file", name, size, eTag = "\"version\"", file = new { mimeType = "application/pdf" }, parentReference = new { id = parentId, driveId = Drive } }, HttpStatusCode.Created);
    private static HttpResponseMessage Json(object value, HttpStatusCode status = HttpStatusCode.OK) => new(status) { Content = new StringContent(JsonSerializer.Serialize(value), Encoding.UTF8, "application/json") };
    private sealed class FakeTokens : IGraphApplicationTokenProvider
    { public ValueTask<string> GetAsync(CancellationToken cancellationToken) => ValueTask.FromResult("test-app-token"); }
    private sealed class FakeCredential(bool fail) : TokenCredential
    {
        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) => throw new NotSupportedException();
        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            Assert.Equal("https://graph.microsoft.com/.default", Assert.Single(requestContext.Scopes));
            if (fail) throw new AuthenticationFailedException("secret provider detail");
            return ValueTask.FromResult(new AccessToken("app", DateTimeOffset.UtcNow.AddMinutes(5)));
        }
    }
    private sealed class RejectScanner : IFileContentScanner
    { public Task<FileScanResult> ScanAsync(FileUpload upload, Stream content, CancellationToken cancellationToken) => Task.FromResult(new FileScanResult(FileScanStatus.Rejected)); }
    private sealed class FakeGraph : HttpMessageHandler
    {
        public Queue<Func<HttpRequestMessage, Task<HttpResponseMessage>>> Replies { get; } = new();
        public List<(HttpMethod Method, string Host, string? Authorization)> Requests { get; } = [];
        public void Add(Func<HttpRequestMessage, HttpResponseMessage> reply) => Replies.Enqueue(request => Task.FromResult(reply(request)));
        public void Add(Func<HttpRequestMessage, Task<HttpResponseMessage>> reply) => Replies.Enqueue(reply);
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        { Requests.Add((request.Method, request.RequestUri!.Host, request.Headers.Authorization?.ToString())); Assert.NotEmpty(Replies); return Replies.Dequeue()(request); }
    }
}
