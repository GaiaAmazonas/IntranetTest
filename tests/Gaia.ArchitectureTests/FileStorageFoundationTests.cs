using Gaia.BuildingBlocks.Files;
using Gaia.Api.Infrastructure.Files;
using Microsoft.Extensions.Configuration;

namespace Gaia.ArchitectureTests;

public sealed class FileStorageFoundationTests
{
    private static readonly HashSet<string> Extensions = [".pdf"];
    private static readonly HashSet<string> MimeTypes = ["application/pdf"];

    [Fact]
    public void UploadNamesUseConsumerSuppliedStableIdentifier()
    {
        var fileId = Guid.NewGuid();
        var upload = new FileUpload(new("Documents", "stable-id"), "informe.pdf", "application/pdf", 20,
            fileId, DateTimeOffset.UtcNow);
        var first = FileUploadRules.ValidateAndCreateStoredName(upload, 100, Extensions, MimeTypes);
        var second = FileUploadRules.ValidateAndCreateStoredName(upload, 100, Extensions, MimeTypes);
        Assert.Equal(first, second);
        Assert.Equal($"{fileId:N}.pdf", first);
        Assert.EndsWith(".pdf", first);
        Assert.DoesNotContain("informe", first, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("../secret.pdf")]
    [InlineData("..\\secret.pdf")]
    [InlineData("/secret.pdf")]
    [InlineData("%2e%2e.pdf")]
    [InlineData("secret.pdf\n")]
    [InlineData(" secret.pdf")]
    public void UnsafeFileNamesAreRejected(string name)
    {
        var error = Assert.Throws<FileStorageException>(() => FileUploadRules.ValidateAndCreateStoredName(
            Upload(name, "application/pdf", 20), 100, Extensions, MimeTypes));
        Assert.Equal(FileStorageError.InvalidFileName, error.Code);
    }

    [Theory]
    [InlineData("../root")]
    [InlineData("root//folder")]
    [InlineData("root\\folder")]
    [InlineData("root/%2f/folder")]
    [InlineData("/root")]
    [InlineData("root/")]
    public void UnsafeRootsAreRejected(string root) =>
        Assert.Throws<FileStorageException>(() => FileUploadRules.NormalizeRoot(root));

    [Fact]
    public void ScopeCannotEscapeRoot() => Assert.Throws<FileStorageException>(() =>
        FileUploadRules.ValidateScope(new("Documents", "../other")));

    [Theory]
    [InlineData(0, FileStorageError.InvalidLength)]
    [InlineData(-1, FileStorageError.InvalidLength)]
    [InlineData(101, FileStorageError.FileTooLarge)]
    public void InvalidSizesAreRejected(long size, FileStorageError expected)
    {
        var error = Assert.Throws<FileStorageException>(() => FileUploadRules.ValidateAndCreateStoredName(
            Upload("file.pdf", "application/pdf", size), 100, Extensions, MimeTypes));
        Assert.Equal(expected, error.Code);
    }

    [Theory]
    [InlineData("file.exe", "application/pdf")]
    [InlineData("file.pdf", "application/octet-stream")]
    public void BothAllowlistsMustMatch(string name, string mime)
    {
        var error = Assert.Throws<FileStorageException>(() => FileUploadRules.ValidateAndCreateStoredName(
            Upload(name, mime, 20), 100, Extensions, MimeTypes));
        Assert.Equal(FileStorageError.TypeNotAllowed, error.Code);
    }

    [Fact]
    public void IntegrationIsDisabledWithoutConfiguration() => Assert.False(
        SharePointStorageConfiguration.From(new ConfigurationBuilder().Build(), "Production").Enabled);

    [Fact]
    public void ValidConfigurationIsAcceptedWithoutRemoteOperations()
    {
        var result = SharePointStorageConfiguration.From(Configuration(), "Development");
        Assert.True(result.Enabled);
        Assert.Equal("Gaia/Files", result.RootFolder);
        Assert.Equal(StorageAuthenticationMethod.ManagedIdentity, result.AuthenticationMethod);
        Assert.Contains(".pdf", result.AllowedExtensions);
    }

    [Theory]
    [InlineData("AuthenticationMethod", "ClientSecret", "Production")]
    [InlineData("AuthenticationMethod", "invalid", "Development")]
    [InlineData("TenantId", "secret-value", "Development")]
    [InlineData("SimpleUploadThresholdBytes", "1001", "Development")]
    [InlineData("AllowedExtensions:0", "*", "Development")]
    [InlineData("AllowedMimeTypes:0", "*/*", "Development")]
    [InlineData("RealTestsEnabled", "true", "Development")]
    [InlineData("SiteId", "https://example.org", "Development")]
    public void InvalidConfigurationFailsClosed(string key, string value, string environment)
    {
        var error = Assert.Throws<FileStorageException>(() => SharePointStorageConfiguration.From(Configuration(key, value), environment));
        Assert.Equal(FileStorageError.MissingConfiguration, error.Code);
        Assert.DoesNotContain("secret-value", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DownloadOwnsItsStream()
    {
        var stream = new MemoryStream();
        var metadata = new StoredFile(new("test", "repository", "container", "file"), "etag", "original.pdf", "stored.pdf", "application/pdf", 0);
        await using (var download = new FileDownload(metadata, stream)) Assert.Same(stream, download.Content);
        Assert.False(stream.CanRead);
    }

    private static IConfiguration Configuration(string? key = null, string? value = null)
    {
        var values = new Dictionary<string, string?>
        {
            ["FileStorage:SharePoint:Enabled"] = "true",
            ["FileStorage:SharePoint:TenantId"] = "11111111-1111-1111-1111-111111111111",
            ["FileStorage:SharePoint:ClientId"] = "22222222-2222-2222-2222-222222222222",
            ["FileStorage:SharePoint:AuthenticationMethod"] = "ManagedIdentity",
            ["FileStorage:SharePoint:SiteId"] = "tenant.sharepoint.com,site-guid,web-guid",
            ["FileStorage:SharePoint:DriveId"] = "drive-id",
            ["FileStorage:SharePoint:RootFolder"] = "Gaia/Files",
            ["FileStorage:SharePoint:MaximumFileBytes"] = "1000",
            ["FileStorage:SharePoint:SimpleUploadThresholdBytes"] = "100",
            ["FileStorage:SharePoint:AllowedExtensions:0"] = ".pdf",
            ["FileStorage:SharePoint:AllowedMimeTypes:0"] = "application/pdf"
        };
        if (key is not null) values["FileStorage:SharePoint:" + key] = value;
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }

    private static FileUpload Upload(string name, string mime, long length) =>
        new(new("Documents", "id"), name, mime, length, Guid.NewGuid(), DateTimeOffset.UtcNow);
}
